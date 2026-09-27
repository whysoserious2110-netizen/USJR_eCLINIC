using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AdministrativeNoteService
{
    public static AdministrativeNoteService Instance { get; } = new AdministrativeNoteService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private AdministrativeNoteService()
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
            await _db.CreateTableAsync<AdministrativeNote>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task AddAsync(AdministrativeNote note)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(note);
    }

    public async Task<List<AdministrativeNote>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<AdministrativeNote>().ToListAsync();
        return all
            .Where(n => n.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.DateCreated)
            .ToList();
    }
}