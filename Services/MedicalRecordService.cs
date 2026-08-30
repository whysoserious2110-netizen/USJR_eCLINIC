using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class MedicalRecordService
{
    public static MedicalRecordService Instance { get; } = new MedicalRecordService();

    private readonly SQLiteAsyncConnection _db;

    private MedicalRecordService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<ConsultationRecord>().Wait();
    }

    public async Task<List<ConsultationRecord>> GetForPatientAsync(string patientEmail)
    {
        var all = await _db.Table<ConsultationRecord>().ToListAsync();
        return all
            .Where(r => r.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ConsultationDate)
            .ToList();
    }

    // TODO: called by Doctor's Consultation screen once that's built
    public async Task AddAsync(ConsultationRecord record) => await _db.InsertAsync(record);
}