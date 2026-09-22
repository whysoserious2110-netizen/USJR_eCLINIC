using SQLite;

namespace USJR_eCLINIC.Models;

public class PasswordResetRequest
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int UserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public string CodeSalt { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public int FailedAttempts { get; set; }

    public bool IsVerified { get; set; }

    public bool IsUsed { get; set; }

    public string ResetTokenHash { get; set; } = string.Empty;

    public DateTime? ResetTokenExpiresAtUtc { get; set; }
}