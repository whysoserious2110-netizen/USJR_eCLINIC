using SQLite;

namespace USJR_eCLINIC.Models;

public class VitalSigns
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int AppointmentId { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public string BloodPressure { get; set; } = string.Empty;
    public string HeartRate { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
    public string RespiratoryRate { get; set; } = string.Empty;
    public string OxygenSaturation { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string InitialAssessment { get; set; } = string.Empty;
    public string NursingNotes { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
    public DateTime DateRecorded { get; set; } = DateTime.Now;
}