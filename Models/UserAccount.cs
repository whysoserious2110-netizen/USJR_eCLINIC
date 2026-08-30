using SQLite;

namespace USJR_eCLINIC.Models;

public class UserAccount
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string ProgramOrDepartment { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;

    public string BloodType { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string Allergies { get; set; } = string.Empty;
    public string MedicalConditions { get; set; } = string.Empty;
    public string CurrentMedications { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactNumber { get; set; } = string.Empty;
    public string EmergencyContactRelationship { get; set; } = string.Empty;

    public string ProfileImagePath { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public string LicenseNumber { get; set; } = string.Empty; // PRC License
    public string YearsOfExperience { get; set; } = string.Empty;
    public string ConsultationSchedule { get; set; } = string.Empty; // e.g., "Mon-Fri, 8AM-5PM"

    public string Position { get; set; } = string.Empty; // e.g., "University Physician"
    public string EmploymentStatus { get; set; } = "Active"; // Active, On Leave, Inactive
}