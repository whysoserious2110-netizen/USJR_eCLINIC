using System.Security.Cryptography;
using System.Text;
using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public sealed record EmailCodeSendResult(bool Success, string Message, int RetryAfterSeconds = 0);

public sealed class EmailVerificationService
{
    public static EmailVerificationService Instance { get; } = new();
    private const int MaxAttempts = 5;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);
    private readonly SQLiteAsyncConnection _db = ClinicDatabase.Connection;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private EmailVerificationService() { }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _db.CreateTableAsync<EmailVerificationRequest>();

            // CreateTableAsync does not add columns to an existing SQLite table.
            // Migrate local databases created before email binding was added.
            var columns = await _db.GetTableInfoAsync(nameof(EmailVerificationRequest));
            if (!columns.Any(column => column.Name.Equals(
                    nameof(EmailVerificationRequest.VerifiedEmail),
                    StringComparison.OrdinalIgnoreCase)))
            {
                await _db.ExecuteAsync(
                    "ALTER TABLE EmailVerificationRequest ADD COLUMN VerifiedEmail TEXT NOT NULL DEFAULT ''");
            }

            _initialized = true;
        }
        finally { _initLock.Release(); }
    }

    public async Task<EmailCodeSendResult> StartVerificationAsync(int userId)
    {
        await EnsureInitializedAsync();
        var account = await AuthService.Instance.GetAccountByDatabaseIdAsync(userId);
        if (account == null || account.EmailVerified)
            return new(false, "This account does not need email verification.");

        var previous = await _db.Table<EmailVerificationRequest>()
            .Where(r => r.UserId == userId).FirstOrDefaultAsync();
        var now = DateTime.UtcNow;
        if (previous != null && previous.ResendAvailableAtUtc > now)
            return new(false, "Please wait before requesting another code.",
                (int)Math.Ceiling((previous.ResendAvailableAtUtc - now).TotalSeconds));

        if (!await PasswordResetEmailApiService.Instance.CheckHealthAsync())
            return new(false, "The local email service is unavailable. Start the Email API and try again.");

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var salt = RandomNumberGenerator.GetBytes(16);
        var request = new EmailVerificationRequest
        {
            UserId = userId,
            VerifiedEmail = NormalizeEmail(account.Email),
            CodeSalt = Convert.ToBase64String(salt),
            CodeHash = HashCode(code, salt),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(CodeLifetime),
            ResendAvailableAtUtc = now.Add(Cooldown),
            FailedAttempts = 0,
            IsUsed = false
        };

        if (previous == null)
            await _db.InsertAsync(request);
        else
            await _db.UpdateAsync(request);

        try
        {
            await PasswordResetEmailApiService.Instance.SendRegistrationVerificationCodeAsync(account.Email, code);
            return new(true, "A verification code was sent to your registered email address.");
        }
        catch
        {
            await _db.DeleteAsync(request);
            return new(false, "We could not send the email. Check the local Email API and try again.");
        }
    }

    public async Task<(bool Success, string Message)> VerifyAsync(int userId, string enteredCode)
    {
        await EnsureInitializedAsync();
        var request = await _db.Table<EmailVerificationRequest>()
            .Where(r => r.UserId == userId && !r.IsUsed).FirstOrDefaultAsync();
        var account = await AuthService.Instance.GetAccountByDatabaseIdAsync(userId);
        if (request == null || account == null || account.EmailVerified)
            return (false, "That code is invalid or expired. Request a new code.");

        if (!string.Equals(
                request.VerifiedEmail,
                NormalizeEmail(account.Email),
                StringComparison.Ordinal))
        {
            // The stored email changed after this code was sent. Invalidate the
            // stale code and allow a fresh code to be sent to the current email.
            request.IsUsed = true;
            request.CodeHash = string.Empty;
            request.CodeSalt = string.Empty;
            request.ResendAvailableAtUtc = DateTime.UtcNow;
            await _db.UpdateAsync(request);
            return (false, "The registered email changed after this code was sent. Request a new code for the current email.");
        }

        if (request.ExpiresAtUtc <= DateTime.UtcNow)
            return (false, "That code expired. Request a new one.");
        if (request.FailedAttempts >= MaxAttempts)
            return (false, "Too many incorrect attempts. Request a new code.");

        byte[] salt;
        try { salt = Convert.FromBase64String(request.CodeSalt); }
        catch { return (false, "The verification request is invalid. Request a new code."); }

        if (!FixedTimeEquals(HashCode(enteredCode.Trim(), salt), request.CodeHash))
        {
            request.FailedAttempts++;
            await _db.UpdateAsync(request);
            return (false, request.FailedAttempts >= MaxAttempts
                ? "Too many incorrect attempts. Request a new code."
                : $"The code is incorrect. {MaxAttempts - request.FailedAttempts} attempts remain.");
        }

        account.EmailVerified = true;
        account.AccountStatus = account.Role is "Faculty" or "Non-Teaching"
            ? "PendingStaffIdReview"
            : "Active";
        await _db.UpdateAsync(account);
        request.IsUsed = true;
        request.CodeHash = string.Empty;
        request.CodeSalt = string.Empty;
        await _db.UpdateAsync(request);
        return (true, account.Role is "Faculty" or "Non-Teaching"
            ? "Email verified. Your account is now waiting for an authorized Employee ID review."
            : "Email verified. Your account is active and ready to sign in.");
    }

    private static string HashCode(string code, byte[] salt)
    {
        var input = Encoding.UTF8.GetBytes(code);
        var combined = new byte[salt.Length + input.Length];
        Buffer.BlockCopy(salt, 0, combined, 0, salt.Length);
        Buffer.BlockCopy(input, 0, combined, salt.Length, input.Length);
        return Convert.ToBase64String(SHA256.HashData(combined));
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToUpperInvariant();

    private static bool FixedTimeEquals(string first, string second)
    {
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(first), Convert.FromBase64String(second)); }
        catch { return false; }
    }
}
