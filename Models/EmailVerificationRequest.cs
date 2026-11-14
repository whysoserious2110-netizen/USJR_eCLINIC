using SQLite;

namespace USJR_eCLINIC.Models;

public sealed class EmailVerificationRequest
{
    [PrimaryKey]
    public int UserId { get; set; }

    // Bind this OTP to the address that received it. An OTP becomes invalid
    // if the account's registered email changes before verification.
    public string VerifiedEmail { get; set; } = string.Empty;
    public string CodeSalt { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ResendAvailableAtUtc { get; set; }
    public int FailedAttempts { get; set; }
    public bool IsUsed { get; set; }
}
