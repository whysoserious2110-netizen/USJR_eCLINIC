using SQLite;

namespace USJR_eCLINIC.Models;

public class Prescription
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public DateTime DatePrescribed { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string Instructions { get; set; } = string.Empty;
    public bool IsClinicGiven { get; set; }
    public string PrescribedBy { get; set; } = string.Empty;
    public bool IsDispensed { get; set; }
    public DateTime? DispensedDate { get; set; }
}
