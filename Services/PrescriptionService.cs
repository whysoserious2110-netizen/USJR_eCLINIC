using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class PrescriptionService
{
    public static PrescriptionService Instance { get; } = new PrescriptionService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private PrescriptionService()
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
            await _db.CreateTableAsync<Prescription>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<Prescription>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Prescription>().ToListAsync();
        return all
            .Where(p => p.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.DatePrescribed)
            .ToList();
    }


    public async Task<List<Prescription>> GetClinicGivenUndispensedAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Prescription>().ToListAsync();
        return all.Where(p => p.IsClinicGiven && !p.IsDispensed).OrderByDescending(p => p.DatePrescribed).ToList();
    }

    public async Task<bool> MarkDispensedAsync(int prescriptionId)
    {
        await EnsureInitializedAsync();
        var rx = await _db.Table<Prescription>().Where(p => p.Id == prescriptionId).FirstOrDefaultAsync();
        if (rx == null) return false;

        rx.IsDispensed = true;
        rx.DispensedDate = DateTime.Now;
        await _db.UpdateAsync(rx);

        await NotificationService.Instance.AddAsync(
            rx.PatientEmail,
            "Medication Dispensed",
            $"Your {rx.MedicineName} ({rx.Dosage}) has been dispensed at the clinic.");

        return true;
    }


    public async Task AddAsync(Prescription prescription)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(prescription);
    }
}