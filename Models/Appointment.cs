using SQLite;

namespace USJR_eCLINIC.Models;

public class Appointment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;     // Medical, Dental, Follow-up, Certificate Request
    public string SubService { get; set; } = string.Empty;      // Reason (Medical), Dental Service, Follow-up Reason, Certificate Type
    public string ReasonOrPurpose { get; set; } = string.Empty; // "Other" text, follow-up notes, certificate purpose
    public DateTime VisitDate { get; set; }
    public string VisitTime { get; set; } = string.Empty;
    public string Location { get; set; } = "Main Campus Clinic";
    public string Status { get; set; } = "Pending";
    public bool IsSeenByPatient { get; set; } = false;

    public string CertificateContent { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }
}