using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class ApeLabResultService
{
    public static ApeLabResultService Instance { get; } = new ApeLabResultService();

    private readonly SQLiteAsyncConnection _db;

    private ApeLabResultService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<ApeLabResult>().Wait();
    }

    public async Task AddAsync(ApeLabResult result) => await _db.InsertAsync(result);

    public async Task<List<ApeLabResult>> GetForPatientAsync(string patientEmail)
    {
        var all = await _db.Table<ApeLabResult>().ToListAsync();
        return all.Where(r => r.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(r => r.DateEncoded)
                   .ToList();
    }
}