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
            await _db.RunInTransactionAsync(connection =>
            {
                var columns = connection.GetTableInfo("ConsultationRecord");
                if (!columns.Any(column => column.Name.Equals("AppointmentId", StringComparison.OrdinalIgnoreCase)))
                    connection.Execute("ALTER TABLE ConsultationRecord ADD COLUMN AppointmentId INTEGER NOT NULL DEFAULT 0");
                if (!columns.Any(column => column.Name.Equals("AttendingStaffId", StringComparison.OrdinalIgnoreCase)))
                    connection.Execute("ALTER TABLE ConsultationRecord ADD COLUMN AttendingStaffId INTEGER NOT NULL DEFAULT 0");
                if (!columns.Any(column => column.Name.Equals("FollowUpRecommended", StringComparison.OrdinalIgnoreCase)))
                    connection.Execute("ALTER TABLE ConsultationRecord ADD COLUMN FollowUpRecommended INTEGER NOT NULL DEFAULT 0");
                if (!columns.Any(column => column.Name.Equals("RecommendedFollowUpDate", StringComparison.OrdinalIgnoreCase)))
                    connection.Execute("ALTER TABLE ConsultationRecord ADD COLUMN RecommendedFollowUpDate DATETIME NULL");
            });
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<string>> GetDistinctPatientEmailsForStaffAsync(string attendingStaffName, int staffId = 0)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<ConsultationRecord>().ToListAsync();
        return all
            .Where(record => (staffId > 0 && record.AttendingStaffId == staffId) ||
                             (record.AttendingStaffId == 0 && record.AttendingStaff.Equals(attendingStaffName, StringComparison.OrdinalIgnoreCase)))
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

    public async Task<ConsultationRecord?> GetByAppointmentIdAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<ConsultationRecord>()
            .Where(record => record.AppointmentId == appointmentId)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(ConsultationRecord record)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(record);
    }
}
