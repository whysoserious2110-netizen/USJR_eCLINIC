using SQLite;

namespace USJR_eCLINIC.Models;

public class ConsultationRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public DateTime ConsultationDate { get; set; }
    public string ChiefComplaint { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string FollowUpNotes { get; set; } = string.Empty;
    public string AttendingStaff { get; set; } = string.Empty; // filled in by Doctor later
}