using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class MedicalRecordService
{
    public static MedicalRecordService Instance { get; } = new MedicalRecordService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private MedicalRecordService() => _db = ClinicDatabase.Connection;

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
            .Where(record => record.AttendingStaff.Equals(attendingStaffName, StringComparison.OrdinalIgnoreCase))
            .Select(record => record.PatientEmail)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<string>> GetPatientEmailsWithConsultationsAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<ConsultationRecord>().ToListAsync();
        return all
            .Where(record => !string.IsNullOrWhiteSpace(record.PatientEmail))
            .Select(record => record.PatientEmail.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<DateTime?> GetLastVisitDateAsync(string patientEmail)
    {
        var records = await GetForPatientAsync(patientEmail);
        return records.Count > 0 ? records.Max(record => record.ConsultationDate) : (DateTime?)null;
    }

    public async Task<List<ConsultationRecord>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<ConsultationRecord>().ToListAsync();
        return all
            .Where(record => record.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(record => record.ConsultationDate)
            .ToList();
    }

    public async Task AddAsync(ConsultationRecord record)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(record);
    }
}
