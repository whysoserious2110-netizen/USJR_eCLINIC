using SQLite;

namespace USJR_eCLINIC.Models;

public class AdministrativeNote
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string NoteText { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime DateCreated { get; set; } = DateTime.Now;
}