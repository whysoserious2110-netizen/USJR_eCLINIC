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

    private const string SessionStateKey =
        "usjr_eclinic_session_state";
    private const string ActiveSessionState = "active";
    private const string SignedOutSessionState = "signed-out";

    private readonly SQLiteAsyncConnection _db;

    private bool _initialized;

    private readonly SemaphoreSlim _initLock =
        new(1, 1);

    private SecureSessionService()
    {
        _db = ClinicDatabase.Connection;
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

        try
        {
            await SecureStorage.Default.SetAsync(SessionTokenKey, rawToken);
            await SecureStorage.Default.SetAsync(SessionStateKey, ActiveSessionState);
        }
        catch
        {
            await ClearSessionAsync();
            throw;
        }
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

        string? sessionState;
        try
        {
            sessionState = await SecureStorage.Default.GetAsync(SessionStateKey);
        }
        catch
        {
            await ClearSessionAsync();
            return false;
        }

        // A missing state value belongs to sessions saved by earlier app versions.
        // Keep those sessions working; an explicit sign-out always writes this marker.
        if (string.Equals(sessionState, SignedOutSessionState, StringComparison.Ordinal))
            return false;
        if (sessionState != null && !string.Equals(sessionState, ActiveSessionState, StringComparison.Ordinal))
        {
            await ClearSessionAsync();
            return false;
        }

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

    /// <summary>
    /// Reauthenticates only the account bound to the protected device token, and only
    /// when the identifier entered on the login page matches that same account.
    /// A successful return rotates and renews the credential.
    /// </summary>
    public async Task<bool> ReauthenticateRememberedUserAsync(string identifier)
    {
        var session = await GetValidSessionAsync();
        if (session == null || string.IsNullOrWhiteSpace(identifier))
            return false;

        var account = await AuthService.Instance.GetAccountByDatabaseIdAsync(session.UserId);
        if (account == null || !AccountMatchesIdentifier(account, identifier))
            return false;

        if (!await AuthService.Instance.RestoreSessionAsync(session.UserId))
        {
            await ClearSessionAsync();
            return false;
        }

        try
        {
            var replacementToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            await SecureStorage.Default.SetAsync(SessionTokenKey, replacementToken);

            session.TokenHash = HashToken(replacementToken);
            session.CreatedAt = DateTime.UtcNow;
            session.ExpiresAt = DateTime.UtcNow.AddDays(30);
            session.IsRevoked = false;
            if (await _db.UpdateAsync(session) <= 0)
                throw new InvalidOperationException("The remembered session could not be renewed.");
            await SecureStorage.Default.SetAsync(SessionStateKey, ActiveSessionState);
            return true;
        }
        catch
        {
            await ClearSessionAsync();
            AuthService.Instance.Logout();
            return false;
        }
    }

    /// <summary>
    /// Ends the in-memory session but retains a valid remembered credential for
    /// explicit passwordless reauthentication from the login page.
    /// </summary>
    public async Task SignOutAsync()
    {
        try
        {
            var session = await GetValidSessionAsync();
            if (session == null)
            {
                await ClearSessionAsync();
            }
            else
            {
                try
                {
                    await SecureStorage.Default.SetAsync(SessionStateKey, SignedOutSessionState);
                }
                catch
                {
                    // If the sign-out state cannot be saved, revoke the local
                    // session and discard its credential rather than auto-restoring it.
                    session.IsRevoked = true;
                    try { await _db.UpdateAsync(session); } catch { }
                    try { await ClearSessionAsync(); } catch { }
                }
            }
        }
        catch
        {
            // A failed local session operation must never leave the in-memory user signed in.
            try { await ClearSessionAsync(); } catch { }
        }
        finally { AuthService.Instance.Logout(); }
    }

    private static bool AccountMatchesIdentifier(UserAccount account, string identifier)
    {
        var input = identifier.Trim();
        return string.Equals(account.Email.Trim(), input, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(account.IdNumber.Trim(), input, StringComparison.OrdinalIgnoreCase);
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
            SecureStorage.Default.Remove(SessionStateKey);
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
        {
            await RemoveInvalidSessionAsync();
            return null;
        }

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
            SecureStorage.Default.Remove(SessionStateKey);
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
