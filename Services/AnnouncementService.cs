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
        _db = ClinicDatabase.Connection;
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

    public async Task<bool> AddAsync(Announcement announcement)
    {
        // R.E.A.D.S. Scholars are the clinic student workers responsible
        // for posting announcements. Enforce this here as well as in the UI.
        if (AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
            return false;

        await EnsureInitializedAsync();
        await _db.InsertAsync(announcement);
        return true;
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
