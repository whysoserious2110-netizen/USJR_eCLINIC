using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class VitalSignsService
{
    public static VitalSignsService Instance { get; } = new VitalSignsService();

    private readonly SQLiteAsyncConnection _db;

    private VitalSignsService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<VitalSigns>().Wait();
    }

    public async Task AddAsync(VitalSigns vitals) => await _db.InsertAsync(vitals);

    public async Task<List<VitalSigns>> GetForPatientAsync(string patientEmail)
    {
        var all = await _db.Table<VitalSigns>().ToListAsync();
        return all.Where(v => v.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(v => v.DateRecorded)
                   .ToList();
    }
}