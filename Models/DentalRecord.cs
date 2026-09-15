using SQLite;

namespace USJR_eCLINIC.Models;

public class DentalRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string ExamFindings { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string TreatmentRendered { get; set; } = string.Empty;
    public string CareInstructions { get; set; } = string.Empty; // e.g. "Brush twice daily, schedule next cleaning in 6 months."
    public string AttendingDentist { get; set; } = string.Empty; // filled by Dentist later
    public string DentalNotes { get; set; } = string.Empty;
}