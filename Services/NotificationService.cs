using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class NotificationService
{
    public static NotificationService Instance { get; } = new NotificationService();

    private readonly SQLiteAsyncConnection _db;

    private NotificationService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Notification>().Wait();
    }

    public async Task AddAsync(string recipientEmail, string title, string message)
    {
        await _db.InsertAsync(new Notification
        {
            RecipientEmail = recipientEmail,
            Title = title,
            Message = message,
            DateCreated = DateTime.Now,
            IsRead = false
        });
    }

   



    public async Task<int> GetUnreadCountAsync(string recipientEmail)
    {
        var all = await GetForUserAsync(recipientEmail);
        return all.Count(n => !n.IsRead);
    }

    public async Task MarkAllAsReadAsync(string recipientEmail)
    {
        var all = await GetForUserAsync(recipientEmail);
        foreach (var n in all.Where(n => !n.IsRead))
        {
            n.IsRead = true;
            await _db.UpdateAsync(n);
        }
    }

    public async Task<List<Notification>> GetForUserAsync(string recipientEmail)
    {
        await PurgeExpiredAsync();

        var all = await _db.Table<Notification>().ToListAsync();
        return all
            .Where(n => n.RecipientEmail.Equals(recipientEmail, StringComparison.OrdinalIgnoreCase) && !n.IsDeleted)
            .OrderByDescending(n => n.DateCreated)
            .ToList();
    }

    public async Task SoftDeleteAsync(int notificationId)
    {
        var notif = await _db.Table<Notification>().Where(n => n.Id == notificationId).FirstOrDefaultAsync();
        if (notif == null) return;

        notif.IsDeleted = true;
        notif.DeletedOn = DateTime.Now;
        await _db.UpdateAsync(notif);
    }

    private async Task PurgeExpiredAsync()
    {
        var all = await _db.Table<Notification>().ToListAsync();
        var expired = all.Where(n => n.IsDeleted && n.DeletedOn.HasValue && n.DeletedOn.Value.AddDays(30) <= DateTime.Now);

        foreach (var n in expired)
            await _db.DeleteAsync(n);
    }



}