using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AdministrativeNoteService
{
    public static AdministrativeNoteService Instance { get; } = new AdministrativeNoteService();

    private readonly SQLiteAsyncConnection _db;

    private AdministrativeNoteService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<AdministrativeNote>().Wait();
    }

    public async Task AddAsync(AdministrativeNote note) => await _db.InsertAsync(note);

    public async Task<List<AdministrativeNote>> GetForPatientAsync(string patientEmail)
    {
        var all = await _db.Table<AdministrativeNote>().ToListAsync();
        return all
            .Where(n => n.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.DateCreated)
            .ToList();
    }
}