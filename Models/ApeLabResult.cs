using SQLite;

namespace USJR_eCLINIC.Models;

public class ApeLabResult
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string PatientEmail { get; set; } = string.Empty;
    public string Cbc { get; set; } = string.Empty;
    public string Urinalysis { get; set; } = string.Empty;
    public string ChestXray { get; set; } = string.Empty;
    public string Ecg { get; set; } = string.Empty;
    public string DrugTest { get; set; } = string.Empty;
    public string OverallFindings { get; set; } = string.Empty;
    public string EncodedBy { get; set; } = string.Empty;
    public DateTime DateEncoded { get; set; } = DateTime.Now;
}