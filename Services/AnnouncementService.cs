using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AnnouncementService
{
    public static AnnouncementService Instance { get; } = new AnnouncementService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private AnnouncementService()
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

            await _db.CreateTableAsync<Announcement>();

            var existing = await _db.Table<Announcement>().ToListAsync();
            if (existing.Count == 0)
            {
                await _db.InsertAsync(new Announcement
                {
                    Message = "Annual physical examination is ongoing at the Main Campus Clinic.",
                    DatePosted = DateTime.Now,
                    ExpiresOn = null,
                    IsActive = true,
                    TargetRole = "All"
                });
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task AddAsync(Announcement announcement)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(announcement);
    }

    public async Task<Announcement?> GetActiveForRoleAsync(string role)
    {
        await EnsureInitializedAsync();

        var all = await _db.Table<Announcement>().ToListAsync();

        return all
            .Where(a => a.IsActive
                        && (a.TargetRole == "All" || a.TargetRole == role)
                        && (a.ExpiresOn == null || a.ExpiresOn.Value.Date >= DateTime.Today))
            .OrderByDescending(a => a.DatePosted)
            .FirstOrDefault();
    }
}