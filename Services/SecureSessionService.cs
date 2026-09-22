using SQLite;
using System.Security.Cryptography;
using System.Text;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class SecureSessionService
{
    public static SecureSessionService Instance { get; } =
        new SecureSessionService();

    private const string SessionTokenKey =
        "usjr_eclinic_session_token";

    private readonly SQLiteAsyncConnection _db;

    private bool _initialized;

    private readonly SemaphoreSlim _initLock =
        new(1, 1);

    private SecureSessionService()
    {
        var dbPath = Path.Combine(
            FileSystem.AppDataDirectory,
            "usjr_eclinic.db3");

        _db = new SQLiteAsyncConnection(dbPath);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync();

        try
        {
            if (_initialized)
                return;

            await _db.CreateTableAsync<DeviceSession>();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task CreateSessionAsync(
        UserAccount user)
    {
        await EnsureInitializedAsync();

        // One remembered account per device.
        await _db.DeleteAllAsync<DeviceSession>();

        var tokenBytes =
            RandomNumberGenerator.GetBytes(32);

        var rawToken =
            Convert.ToBase64String(tokenBytes);

        var session = new DeviceSession
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            IsRevoked = false
        };

        await _db.InsertAsync(session);

        await SecureStorage.Default.SetAsync(
            SessionTokenKey,
            rawToken);
    }

    public async Task<UserAccount?>
        GetRememberedUserAsync()
    {
        var session =
            await GetValidSessionAsync();

        if (session == null)
            return null;

        var account =
            await AuthService.Instance
                .GetAccountByDatabaseIdAsync(
                    session.UserId);

        if (account == null)
        {
            await ClearSessionAsync();
            return null;
        }

        // This only returns information for the
        // Welcome Back preview. It does not set
        // AuthService.CurrentUser.
        return account;
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        var session =
            await GetValidSessionAsync();

        if (session == null)
            return false;

        var restored =
            await AuthService.Instance
                .RestoreSessionAsync(session.UserId);

        if (!restored)
        {
            await ClearSessionAsync();
            return false;
        }

        return true;
    }

    public async Task<bool> HasRememberedSessionAsync()
    {
        var session =
            await GetValidSessionAsync();

        return session != null;
    }

    public async Task ClearSessionAsync()
    {
        await EnsureInitializedAsync();

        try
        {
            SecureStorage.Default.Remove(
                SessionTokenKey);
        }
        catch
        {
            // SecureStorage can become unavailable
            // after device-security changes.
        }

        await _db.DeleteAllAsync<DeviceSession>();
    }

    private async Task<DeviceSession?>
        GetValidSessionAsync()
    {
        await EnsureInitializedAsync();

        string? rawToken;

        try
        {
            rawToken =
                await SecureStorage.Default.GetAsync(
                    SessionTokenKey);
        }
        catch
        {
            await RemoveInvalidSessionAsync();
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawToken))
            return null;

        var tokenHash =
            HashToken(rawToken);

        var session =
            await _db.Table<DeviceSession>()
                .Where(s =>
                    s.TokenHash == tokenHash &&
                    !s.IsRevoked)
                .FirstOrDefaultAsync();

        if (session == null)
        {
            await RemoveInvalidSessionAsync();
            return null;
        }

        if (session.ExpiresAt <= DateTime.UtcNow)
        {
            await RemoveInvalidSessionAsync();
            return null;
        }

        return session;
    }

    private async Task RemoveInvalidSessionAsync()
    {
        try
        {
            SecureStorage.Default.Remove(
                SessionTokenKey);
        }
        catch
        {
            // Nothing else is required.
        }

        try
        {
            await _db.DeleteAllAsync<DeviceSession>();
        }
        catch
        {
            // Database cleanup can be retried later.
        }
    }

    private static string HashToken(
        string rawToken)
    {
        var bytes =
            Encoding.UTF8.GetBytes(rawToken);

        var hash =
            SHA256.HashData(bytes);

        return Convert.ToHexString(hash);
    }
}