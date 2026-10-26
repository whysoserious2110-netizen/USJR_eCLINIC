using SQLite;

namespace USJR_eCLINIC.Services;

/// <summary>
/// Provides one serialized SQLite connection for every local service.
/// The app still stores data on this device; this class only centralizes local access.
/// </summary>
public static class ClinicDatabase
{
    private static readonly Lazy<SQLiteAsyncConnection> ConnectionInstance =
        new(() =>
        {
            var databasePath = Path.Combine(
                FileSystem.AppDataDirectory,
                "usjr_eclinic.db3");

            return new SQLiteAsyncConnection(databasePath);
        });

    public static SQLiteAsyncConnection Connection => ConnectionInstance.Value;
}
