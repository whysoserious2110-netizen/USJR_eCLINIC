using System.Security.Cryptography;

namespace USJR_eCLINIC.Services;

public static class PasswordSecurityService
{
    private const string AlgorithmName = "PBKDF2-SHA256";

    private const int IterationCount = 210_000;

    private const int SaltSize = 16;

    private const int HashSize = 32;

    public static string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException(
                "Password cannot be empty.",
                nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            "$",
            AlgorithmName,
            IterationCount.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public static bool VerifyPassword(
        string password,
        string storedPassword)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrWhiteSpace(storedPassword))
        {
            return false;
        }

        if (!IsHashedPassword(storedPassword))
        {
            return false;
        }

        try
        {
            var parts = storedPassword.Split('$');

            if (parts.Length != 4)
                return false;

            if (!int.TryParse(
                parts[1],
                out var iterations))
            {
                return false;
            }

            if (iterations <= 0)
                return false;

            var salt =
                Convert.FromBase64String(parts[2]);

            var expectedHash =
                Convert.FromBase64String(parts[3]);

            var actualHash =
                Rfc2898DeriveBytes.Pbkdf2(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA256,
                    expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsHashedPassword(
        string? storedPassword)
    {
        if (string.IsNullOrWhiteSpace(storedPassword))
            return false;

        return storedPassword.StartsWith(
            $"{AlgorithmName}$",
            StringComparison.Ordinal);
    }
}