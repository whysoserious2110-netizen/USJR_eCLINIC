using SQLite;
using System.Security.Cryptography;
using System.Text;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public sealed class PasswordResetStartResult
{
    public bool AccountFound { get; init; }

    public string DemoCode { get; init; } = string.Empty;

    public string RecoveryDisplay { get; init; } = string.Empty;
}

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
        var dbPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "usjr_eclinic.db3");

        _db = new SQLiteAsyncConnection(dbPath);
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

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<PasswordResetStartResult>
        StartResetAsync(string studentId)
    {
        await EnsureInitializedAsync();

        var account =
            await AuthService.Instance
                .GetAccountByIdNumberAsync(studentId);

        if (account == null ||
            account.Role != "Student")
        {
            // The UI should still show a generic message
            // so it does not reveal registered accounts.
            return new PasswordResetStartResult
            {
                AccountFound = false
            };
        }

        var existingRequests =
            await _db.Table<PasswordResetRequest>()
                .Where(r => r.UserId == account.Id)
                .ToListAsync();

        foreach (var existing in existingRequests)
        {
            await _db.DeleteAsync(existing);
        }

        var code =
            RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();

        var salt =
            RandomNumberGenerator.GetBytes(16);

        var request = new PasswordResetRequest
        {
            UserId = account.Id,

            CodeSalt =
                Convert.ToBase64String(salt),

            CodeHash =
                HashVerificationCode(code, salt),

            CreatedAtUtc =
                DateTime.UtcNow,

            ExpiresAtUtc =
                DateTime.UtcNow.AddMinutes(10),

            FailedAttempts = 0,
            IsVerified = false,
            IsUsed = false
        };

        await _db.InsertAsync(request);

        return new PasswordResetStartResult
        {
            AccountFound = true,

            // Prototype only. A production version sends
            // this through the university email service.
            DemoCode = code,

            RecoveryDisplay =
                MaskEmail(account.Email)
        };
    }

    public async Task<PasswordResetVerificationResult>
        VerifyCodeAsync(
            string studentId,
            string enteredCode)
    {
        await EnsureInitializedAsync();

        var account =
            await AuthService.Instance
                .GetAccountByIdNumberAsync(studentId);

        if (account == null ||
            account.Role != "Student")
        {
            return FailedVerification(
                "The verification code is invalid or expired.");
        }

        var requests =
            await _db.Table<PasswordResetRequest>()
                .Where(r =>
                    r.UserId == account.Id &&
                    !r.IsUsed)
                .ToListAsync();

        var request = requests
            .OrderByDescending(r => r.CreatedAtUtc)
            .FirstOrDefault();

        if (request == null)
        {
            return FailedVerification(
                "The verification code is invalid or expired.");
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

    private static string MaskEmail(
        string email)
    {
        if (string.IsNullOrWhiteSpace(email) ||
            !email.Contains('@'))
        {
            return "registered recovery method";
        }

        var parts = email.Split('@');

        var name = parts[0];
        var domain = parts[1];

        var visibleName =
            name.Length <= 1
                ? "*"
                : $"{name[0]}{new string('*',
                    Math.Max(2, name.Length - 1))}";

        return $"{visibleName}@{domain}";
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