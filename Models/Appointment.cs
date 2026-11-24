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
    public int? OriginalAppointmentId { get; set; }
    public int? RecommendedByStaffId { get; set; }
    public DateTime? RecommendedFollowUpDate { get; set; }
    public string FollowUpInstructions { get; set; } = string.Empty;
    public int? ReviewingStaffId { get; set; }
    public string ReviewRemarks { get; set; } = string.Empty;
    public DateTime? ProposedVisitDate { get; set; }
    public string ProposedVisitTime { get; set; } = string.Empty;
    public string SuggestedScheduleStatus { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string VisitTime { get; set; } = string.Empty;
    public string AssignedShift { get; set; } = string.Empty;
    public int? RescheduledFromAppointmentId { get; set; }
    public int? RescheduledToAppointmentId { get; set; }
    public string ReschedulingReason { get; set; } = string.Empty;
    public DateTime? RescheduledAt { get; set; }
    [Indexed]
    public int? AssignedStaffId { get; set; }
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
