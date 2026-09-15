using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class MedicineStockService
{
    public static MedicineStockService Instance { get; } = new MedicineStockService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private MedicineStockService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _db.CreateTableAsync<MedicineStock>();

            var existing = await _db.Table<MedicineStock>().ToListAsync();
            if (existing.Count == 0)
            {
                var seedItems = new[]
                {
                    new MedicineStock { MedicineName = "Paracetamol 500mg", Quantity = 120, Unit = "tablets", LowStockThreshold = 20 },
                    new MedicineStock { MedicineName = "Mefenamic Acid 500mg", Quantity = 80, Unit = "tablets", LowStockThreshold = 20 },
                    new MedicineStock { MedicineName = "Cetirizine 10mg", Quantity = 15, Unit = "tablets", LowStockThreshold = 20 },
                    new MedicineStock { MedicineName = "Povidone Iodine", Quantity = 8, Unit = "bottles", LowStockThreshold = 10 },
                    new MedicineStock { MedicineName = "Gauze Pads", Quantity = 200, Unit = "pcs", LowStockThreshold = 50 },
                };
                foreach (var item in seedItems)
                    await _db.InsertAsync(item);
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<MedicineStock>> GetAllAsync()
    {
        await EnsureInitializedAsync();
        return (await _db.Table<MedicineStock>().ToListAsync()).OrderBy(m => m.MedicineName).ToList();
    }

    public async Task AddStockAsync(int id, int amountToAdd)
    {
        await EnsureInitializedAsync();
        var item = await _db.Table<MedicineStock>().Where(m => m.Id == id).FirstOrDefaultAsync();
        if (item == null) return;

        item.Quantity += amountToAdd;
        item.LastUpdated = DateTime.Now;
        await _db.UpdateAsync(item);
    }

    public async Task AddNewMedicineAsync(MedicineStock item)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(item);
    }

    public async Task<bool> DeductStockAsync(string medicineName, int amount)
    {
        await EnsureInitializedAsync();
        var item = await _db.Table<MedicineStock>()
            .Where(m => m.MedicineName.Equals(medicineName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefaultAsync();

        if (item == null || item.Quantity < amount) return false;

        item.Quantity -= amount;
        item.LastUpdated = DateTime.Now;
        await _db.UpdateAsync(item);
        return true;
    }
}