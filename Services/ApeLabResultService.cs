using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class ApeLabResultService
{
    public static ApeLabResultService Instance { get; } = new ApeLabResultService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private ApeLabResultService()
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
            await _db.CreateTableAsync<ApeLabResult>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task AddAsync(ApeLabResult result)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(result);
    }

    public async Task<List<ApeLabResult>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<ApeLabResult>().ToListAsync();
        return all.Where(r => r.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(r => r.DateEncoded)
                   .ToList();
    }
}