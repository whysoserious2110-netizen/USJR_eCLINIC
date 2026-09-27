using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class VitalSignsService
{
    public static VitalSignsService Instance { get; } = new VitalSignsService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private VitalSignsService()
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
            await _db.CreateTableAsync<VitalSigns>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task AddAsync(VitalSigns vitals)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(vitals);
    }

    public async Task<List<VitalSigns>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<VitalSigns>().ToListAsync();
        return all.Where(v => v.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(v => v.DateRecorded)
                 .ToList();
    }

    public async Task<VitalSigns?> GetForAppointmentAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        if (appointmentId <= 0)
            return null;

        var records = await _db.Table<VitalSigns>()
            .Where(v => v.AppointmentId == appointmentId)
            .ToListAsync();

        return records
            .OrderByDescending(v => v.DateRecorded)
            .FirstOrDefault();
    }

}