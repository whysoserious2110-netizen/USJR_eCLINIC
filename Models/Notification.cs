using SQLite;

namespace USJR_eCLINIC.Models;

public class Notification
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; } = DateTime.Now;
    public bool IsRead { get; set; } = false;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedOn { get; set; }
}