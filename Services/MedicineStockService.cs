using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class MedicineStockService
{
    public static MedicineStockService Instance { get; } = new();

    private readonly SQLiteAsyncConnection _db = ClinicDatabase.Connection;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private MedicineStockService()
    {
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
                return;

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
                    new MedicineStock { MedicineName = "Gauze Pads", Quantity = 200, Unit = "pcs", LowStockThreshold = 50 }
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
        return (await _db.Table<MedicineStock>().ToListAsync())
            .OrderBy(item => item.MedicineName)
            .ToList();
    }

    public async Task<bool> AddStockAsync(int id, int amountToAdd)
    {
        if (amountToAdd <= 0)
            return false;

        await EnsureInitializedAsync();

        var updated = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var item = connection.Find<MedicineStock>(id);
            if (item == null)
                return;

            item.Quantity += amountToAdd;
            item.LastUpdated = DateTime.Now;
            connection.Update(item);
            updated = true;
        });

        return updated;
    }

    public async Task<bool> AddNewMedicineAsync(MedicineStock item)
    {
        if (string.IsNullOrWhiteSpace(item.MedicineName) ||
            string.IsNullOrWhiteSpace(item.Unit) ||
            item.Quantity < 0 ||
            item.LowStockThreshold < 0)
        {
            return false;
        }

        await EnsureInitializedAsync();

        item.MedicineName = item.MedicineName.Trim();
        item.Unit = item.Unit.Trim();

        var added = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var existing = connection.Table<MedicineStock>()
                .ToList()
                .Any(stock => string.Equals(
                    stock.MedicineName.Trim(),
                    item.MedicineName,
                    StringComparison.OrdinalIgnoreCase));

            if (existing)
                return;

            item.LastUpdated = DateTime.Now;
            connection.Insert(item);
            added = true;
        });

        return added;
    }

    public async Task<bool> DeductStockAsync(string medicineName, int amount)
    {
        if (string.IsNullOrWhiteSpace(medicineName) || amount <= 0)
            return false;

        await EnsureInitializedAsync();

        var deducted = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var item = connection.Table<MedicineStock>()
                .ToList()
                .FirstOrDefault(stock => string.Equals(
                    stock.MedicineName.Trim(),
                    medicineName.Trim(),
                    StringComparison.OrdinalIgnoreCase));

            if (item == null || item.Quantity < amount)
                return;

            item.Quantity -= amount;
            item.LastUpdated = DateTime.Now;
            connection.Update(item);
            deducted = true;
        });

        return deducted;
    }
}
