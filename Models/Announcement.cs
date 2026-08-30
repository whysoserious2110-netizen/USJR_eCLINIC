using SQLite;

namespace USJR_eCLINIC.Models;

public class Announcement
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Message { get; set; } = string.Empty;
    public DateTime DatePosted { get; set; } = DateTime.Now;
    public DateTime? ExpiresOn { get; set; }
    public bool IsActive { get; set; } = true;
    public string TargetRole { get; set; } = "All"; // "All", "Student", "Faculty", etc.
}