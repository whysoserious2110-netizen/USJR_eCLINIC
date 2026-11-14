using SQLite;
using System.Security.Cryptography;
using System.Text;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public sealed class PasswordResetVerificationResult
{
    public bool Success { get; init; }

    public string ResetToken { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}

public sealed class PasswordResetOperationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}

public sealed record PasswordResetStartResult(bool Success, int RequestId = 0);

public class PasswordResetService
{
    public static PasswordResetService Instance { get; } =
        new PasswordResetService();

    private const int MaximumAttempts = 5;

    private readonly SQLiteAsyncConnection _db;

    private bool _initialized;

    private readonly SemaphoreSlim _initLock =
        new(1, 1);

    private PasswordResetService()
    {
        _db = ClinicDatabase.Connection;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();

        try
        {
            if (_initialized)
                return;

            await _db.CreateTableAsync<
                PasswordResetRequest>();

            // Add the email binding to existing local SQLite databases.
            var columns = await _db.GetTableInfoAsync(nameof(PasswordResetRequest));
            if (!columns.Any(column => column.Name.Equals(
                    nameof(PasswordResetRequest.RequestedEmail),
                    StringComparison.OrdinalIgnoreCase)))
            {
                await _db.ExecuteAsync(
                    "ALTER TABLE PasswordResetRequest ADD COLUMN RequestedEmail TEXT NOT NULL DEFAULT ''");
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<PasswordResetStartResult>
        StartResetAsync(string email)
    {
        await EnsureInitializedAsync();

        if (!await PasswordResetEmailApiService.Instance.CheckHealthAsync())
            throw new InvalidOperationException("Local email service is unavailable.");

        var account =
            await AuthService.Instance
                .GetAccountByEmailAsync(email.Trim().ToLowerInvariant());

        if (account == null)
            return new(false);

        var existingRequests =
            await _db.Table<PasswordResetRequest>()
                .Where(r => r.UserId == account.Id)
                .ToListAsync();

        var code =
            RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();

        var salt =
            RandomNumberGenerator.GetBytes(16);

        var now = DateTime.UtcNow;
        var request = new PasswordResetRequest
        {
            UserId = account.Id,
            RequestedEmail = NormalizeEmail(account.Email),

            CodeSalt =
                Convert.ToBase64String(salt),

            CodeHash =
                HashVerificationCode(code, salt),

            CreatedAtUtc = now,

            ExpiresAtUtc = now.AddMinutes(10),

            FailedAttempts = 0,
            IsVerified = false,
            IsUsed = false
        };

        // Save the hashed request first. If this write fails, do not send a
        // code that the app cannot verify. Keep the previous request until
        // delivery succeeds so a failed resend does not invalidate it.
        await _db.InsertAsync(request);

        try
        {
            await PasswordResetEmailApiService.Instance
                .SendCodeAsync(account.Email, code);
        }
        catch
        {
            // Delivery failed; remove only the new request and leave any
            // previously delivered code available until it expires.
            await _db.DeleteAsync(request);
            return new(false);
        }

        // The replacement was delivered, so invalidate older reset requests.
        foreach (var existing in existingRequests)
            await _db.DeleteAsync(existing);

        return new(true, request.Id);
    }

    public async Task<PasswordResetVerificationResult>
        VerifyCodeAsync(
            int requestId,
            string enteredCode)
    {
        await EnsureInitializedAsync();

        var request = requestId > 0
            ? await _db.Table<PasswordResetRequest>()
                .Where(r => r.Id == requestId && !r.IsUsed)
                .FirstOrDefaultAsync()
            : null;

        if (request == null)
        {
            return FailedVerification(
                "No active reset request was found. Go back, enter the email linked to this account, and request a fresh code.");
        }

        var account = await AuthService.Instance.GetAccountByDatabaseIdAsync(request.UserId);
        if (account == null || !string.Equals(
                NormalizeEmail(account.Email),
                NormalizeEmail(request.RequestedEmail),
                StringComparison.Ordinal))
        {
            request.IsUsed = true;
            request.CodeHash = string.Empty;
            request.CodeSalt = string.Empty;
            await _db.UpdateAsync(request);
            return FailedVerification(
                "No active reset request was found. Go back, enter the email linked to this account, and request a fresh code.");
        }

        if (request.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return FailedVerification(
                "The verification code has expired.");
        }

        if (request.FailedAttempts >= MaximumAttempts)
        {
            return FailedVerification(
                "Too many incorrect attempts. Request a new code.");
        }

        byte[] salt;

        try
        {
            salt =
                Convert.FromBase64String(
                    request.CodeSalt);
        }
        catch
        {
            return FailedVerification(
                "The verification request is invalid.");
        }

        var enteredHash =
            HashVerificationCode(
                enteredCode.Trim(),
                salt);

        var isValid =
            FixedTimeHashEquals(
                enteredHash,
                request.CodeHash);

        if (!isValid)
        {
            request.FailedAttempts++;

            await _db.UpdateAsync(request);

            var attemptsRemaining =
                MaximumAttempts -
                request.FailedAttempts;

            return FailedVerification(
                attemptsRemaining > 0
                    ? $"Incorrect code. {attemptsRemaining} " +
                      "attempts remaining."
                    : "Too many incorrect attempts. " +
                      "Request a new code.");
        }

        var rawResetToken =
            Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32));

        request.IsVerified = true;

        request.ResetTokenHash =
            HashResetToken(rawResetToken);

        request.ResetTokenExpiresAtUtc =
            DateTime.UtcNow.AddMinutes(10);

        await _db.UpdateAsync(request);

        return new PasswordResetVerificationResult
        {
            Success = true,
            ResetToken = rawResetToken,
            Message = "Verification successful."
        };
    }

    public async Task<PasswordResetOperationResult>
        ResetPasswordAsync(
            string resetToken,
            string newPassword)
    {
        await EnsureInitializedAsync();

        if (!IsStrongPassword(newPassword))
        {
            return new PasswordResetOperationResult
            {
                Success = false,
                Message =
                    "Password must be at least 8 characters " +
                    "and contain uppercase, lowercase, number, " +
                    "and special characters."
            };
        }

        if (string.IsNullOrWhiteSpace(resetToken))
        {
            return new PasswordResetOperationResult
            {
                Success = false,
                Message =
                    "The password-reset session is invalid."
            };
        }

        var tokenHash =
            HashResetToken(resetToken);

        var requests =
            await _db.Table<PasswordResetRequest>()
                .Where(r =>
                    r.IsVerified &&
                    !r.IsUsed)
                .ToListAsync();

        var request = requests.FirstOrDefault(r =>
            string.Equals(
                r.ResetTokenHash,
                tokenHash,
                StringComparison.Ordinal));

        if (request == null ||
            request.ResetTokenExpiresAtUtc == null ||
            request.ResetTokenExpiresAtUtc <=
            DateTime.UtcNow)
        {
            return new PasswordResetOperationResult
            {
                Success = false,
                Message =
                    "The password-reset session has expired. " +
                    "Please request a new code."
            };
        }

        var updated =
            await AuthService.Instance
                .UpdatePasswordAsync(
                    request.UserId,
                    newPassword);

        if (!updated)
        {
            return new PasswordResetOperationResult
            {
                Success = false,
                Message =
                    "The password could not be updated."
            };
        }

        request.IsUsed = true;
        request.ResetTokenHash = string.Empty;
        request.ResetTokenExpiresAtUtc = null;

        await _db.UpdateAsync(request);

        // Revoke the old remembered login after
        // changing the password.
        await SecureSessionService.Instance
            .ClearSessionAsync();

        AuthService.Instance.Logout();

        return new PasswordResetOperationResult
        {
            Success = true,
            Message =
                "Your password has been reset successfully."
        };
    }

    private static string HashVerificationCode(
        string code,
        byte[] salt)
    {
        var codeBytes =
            Encoding.UTF8.GetBytes(code);

        var combined =
            new byte[salt.Length + codeBytes.Length];

        Buffer.BlockCopy(
            salt,
            0,
            combined,
            0,
            salt.Length);

        Buffer.BlockCopy(
            codeBytes,
            0,
            combined,
            salt.Length,
            codeBytes.Length);

        var hash =
            SHA256.HashData(combined);

        return Convert.ToBase64String(hash);
    }

    private static string NormalizeEmail(string? email) =>
        (email ?? string.Empty).Trim().ToUpperInvariant();

    private static string HashResetToken(
        string token)
    {
        var bytes =
            Encoding.UTF8.GetBytes(token);

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }

    private static bool FixedTimeHashEquals(
        string firstHash,
        string secondHash)
    {
        try
        {
            var first =
                Convert.FromBase64String(firstHash);

            var second =
                Convert.FromBase64String(secondHash);

            return CryptographicOperations
                .FixedTimeEquals(first, second);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStrongPassword(
        string password)
    {
        if (password.Length < 8)
            return false;

        return password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit) &&
               password.Any(
                   character =>
                       !char.IsLetterOrDigit(character));
    }

    private static PasswordResetVerificationResult
        FailedVerification(string message)
    {
        return new PasswordResetVerificationResult
        {
            Success = false,
            Message = message
        };
    }
}
