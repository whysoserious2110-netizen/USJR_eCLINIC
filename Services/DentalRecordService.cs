using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class DentalRecordService
{
    public static DentalRecordService Instance { get; } = new DentalRecordService();

    private readonly SQLiteAsyncConnection _db;

    private DentalRecordService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<DentalRecord>().Wait();
    }

    public async Task<List<string>> GetDistinctPatientEmailsForStaffAsync(string attendingDentistName)
    {
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
        var all = await _db.Table<DentalRecord>().ToListAsync();
        return all
            .Where(r => r.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.VisitDate)
            .ToList();
    }

    // TODO: called by Dentist's Examination screen once that's built
    public async Task AddAsync(DentalRecord record) => await _db.InsertAsync(record);
}