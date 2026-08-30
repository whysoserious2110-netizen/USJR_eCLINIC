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

    public async Task<List<Notification>> GetForUserAsync(string recipientEmail)
    {
        var all = await _db.Table<Notification>().ToListAsync();
        return all
            .Where(n => n.RecipientEmail.Equals(recipientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.DateCreated)
            .ToList();
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



}