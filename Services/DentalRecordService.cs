using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public sealed class DentalRecordColumnInfo
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

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

            var columns = await _db.QueryAsync<DentalRecordColumnInfo>("PRAGMA table_info('DentalRecord');");
            if (!columns.Any(column => column.Name.Equals("AppointmentId", StringComparison.OrdinalIgnoreCase)))
            {
                await _db.ExecuteAsync(
                    "ALTER TABLE DentalRecord ADD COLUMN AppointmentId INTEGER NOT NULL DEFAULT 0");
            }

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

    public async Task<bool> AddForAppointmentAndCompleteAsync(DentalRecord record)
    {
        var dentist = AuthService.Instance.CurrentUser;
        if (dentist?.Role != "Dentist" || record.AppointmentId <= 0)
            return false;

        await EnsureInitializedAsync();

        // Initialize the appointment table and any existing appointment workflow columns.
        await AppointmentService.Instance.GetByIdAsync(record.AppointmentId);

        var saved = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(record.AppointmentId);
            if (appointment == null ||
                appointment.VisitDate.Date != DateTime.Today ||
                appointment.Status != "VitalsRecorded" ||
                (appointment.ServiceType != "Dental" &&
                 !(appointment.ServiceType == "Cert Request" && appointment.SubService == "Dental Certificate")))
            {
                return;
            }

            var existingRecord = connection.Table<DentalRecord>()
                .Where(existing => existing.AppointmentId == record.AppointmentId)
                .FirstOrDefault();
            if (existingRecord != null)
                return;

            record.PatientEmail = appointment.PatientEmail;
            record.VisitDate = DateTime.Now;
            record.AttendingDentist = dentist.FullName;
            connection.Insert(record);

            appointment.Status = "Completed";
            if (appointment.ServiceType == "Cert Request")
                appointment.CertificateStatus = "Awaiting Clinical Review";

            connection.Update(appointment);
            saved = true;
        });

        return saved;
    }
}
