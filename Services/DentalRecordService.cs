using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class DentalRecordService
{
    public static DentalRecordService Instance { get; } = new DentalRecordService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private DentalRecordService() => _db = ClinicDatabase.Connection;

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
        return all
            .Where(record => record.AttendingDentist.Equals(attendingDentistName, StringComparison.OrdinalIgnoreCase))
            .Select(record => record.PatientEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<string>> GetPatientEmailsWithRecordsAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<DentalRecord>().ToListAsync();
        return all
            .Where(record => !string.IsNullOrWhiteSpace(record.PatientEmail))
            .Select(record => record.PatientEmail.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<DateTime?> GetLastVisitDateAsync(string patientEmail)
    {
        var records = await GetForPatientAsync(patientEmail);
        return records.Count > 0 ? records.Max(record => record.VisitDate) : (DateTime?)null;
    }

    public async Task<List<DentalRecord>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<DentalRecord>().ToListAsync();
        return all
            .Where(record => record.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.VisitDate)
            .ToList();
    }

    public async Task AddAsync(DentalRecord record)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(record);
    }
}
