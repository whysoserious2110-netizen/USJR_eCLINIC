using SQLite;

namespace USJR_eCLINIC.Models;

public class Appointment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;     // Medical, Dental, Follow-up, Certificate Request
    public string SubService { get; set; } = string.Empty;      // Medical/dental certificate type or appointment service
    public string ReasonOrPurpose { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string VisitTime { get; set; } = string.Empty;
    public string Location { get; set; } = "Main Campus Clinic";
    public string Status { get; set; } = "Pending";
    public bool IsSeenByPatient { get; set; } = false;

    // Paper-certificate workflow is separate from the appointment lifecycle above.
    public string CertificateStatus { get; set; } = string.Empty;
    public string CertificateDecisionNote { get; set; } = string.Empty;
    public string CertificateReviewedBy { get; set; } = string.Empty;
    public DateTime? CertificateReviewedAt { get; set; }
    public string CertificateCollectedBy { get; set; } = string.Empty;
    public DateTime? CertificateCollectedAt { get; set; }

    // Retained for compatibility with older local databases; the paper workflow does not use them.
    public string CertificateContent { get; set; } = string.Empty;
    public DateTime? IssuedDate { get; set; }

    public int? QueueNumber { get; set; }
    public DateTime? CheckInTime { get; set; }
}
