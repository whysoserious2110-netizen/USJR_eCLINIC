using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class MedicalRecordService
{
    public static MedicalRecordService Instance { get; } = new MedicalRecordService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private MedicalRecordService()
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
            await _db.CreateTableAsync<ConsultationRecord>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }


    public async Task<List<string>> GetDistinctPatientEmailsForStaffAsync(string attendingStaffName)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<ConsultationRecord>().ToListAsync();
        return all
            .Where(r => r.AttendingStaff.Equals(attendingStaffName, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.PatientEmail)
            .Distinct()
            .ToList();
    }

    public async Task<DateTime?> GetLastVisitDateAsync(string patientEmail)
    {
        var records = await GetForPatientAsync(patientEmail);
        return records.Count > 0 ? records.Max(r => r.ConsultationDate) : (DateTime?)null;
    }

    public async Task<List<ConsultationRecord>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<ConsultationRecord>().ToListAsync();
        return all
            .Where(r => r.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ConsultationDate)
            .ToList();
    }

    public async Task AddAsync(ConsultationRecord record)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(record);
    }
}