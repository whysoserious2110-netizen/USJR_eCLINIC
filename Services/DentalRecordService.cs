using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class DentalRecordService
{
    public static DentalRecordService Instance { get; } = new DentalRecordService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private DentalRecordService()
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
            await _db.CreateTableAsync<DentalRecord>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<string>> GetDistinctPatientEmailsForStaffAsync(string attendingDentistName)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<DentalRecord>().ToListAsync();
        return all.Where(r => r.AttendingDentist.Equals(attendingDentistName, StringComparison.OrdinalIgnoreCase))
                   .Select(r => r.PatientEmail).Distinct().ToList();
    }

    public async Task<DateTime?> GetLastVisitDateAsync(string patientEmail)
    {
        var records = await GetForPatientAsync(patientEmail);
        return records.Count > 0 ? records.Max(r => r.VisitDate) : (DateTime?)null;
    }




    public async Task<List<DentalRecord>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<DentalRecord>().ToListAsync();
        return all
            .Where(r => r.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.VisitDate)
            .ToList();
    }

    public async Task AddAsync(DentalRecord record)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(record);
    }
}