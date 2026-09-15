using SQLite;

namespace USJR_eCLINIC.Models;

public class MedicineStock
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string MedicineName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Unit { get; set; } = "pcs";
    public int LowStockThreshold { get; set; } = 10;
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}