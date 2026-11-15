using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AuthService
{
    public static AuthService Instance { get; } =
        new AuthService();

    public UserAccount? CurrentUser { get; private set; }

    // Roles that may be created through the public signup screen.
    // All accounts authenticate locally in the MAUI app.
    public static readonly string[] CentralPatientRoles =
    {
        "Student",
        "Faculty",
        "Non-Teaching"
    };

    public static readonly string[] ClinicStaffRoles =
    {
        "Nurse",
        "Doctor",
        "Dentist",
        "R.E.A.D.S. Scholar"
    };

    public static bool IsClinicStaffRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) &&
        ClinicStaffRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public static bool IsClinicAdministrator(UserAccount? account) =>
        account != null &&
        string.Equals(account.Role, "Admin Personnel", StringComparison.OrdinalIgnoreCase) &&
        account.EmailVerified &&
        string.Equals(account.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase);

    private readonly SQLiteAsyncConnection _db;

    private bool _initialized;

    private readonly SemaphoreSlim _initLock =
        new(1, 1);

    private static readonly string[] AllowedEmailDomains =
    {
        "usjr.edu.ph",
        "gmail.com"
    };

    private AuthService()
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

            await _db.CreateTableAsync<UserAccount>();
            await _db.RunInTransactionAsync(connection =>
            {
                var columns = connection.GetTableInfo("UserAccount");
                if (!columns.Any(column =>
                        string.Equals(column.Name, "StudentProgramAndYear", StringComparison.OrdinalIgnoreCase)))
                {
                    connection.Execute(
                        "ALTER TABLE UserAccount ADD COLUMN StudentProgramAndYear TEXT NOT NULL DEFAULT ''");
                }
            });
            await _db.RunInTransactionAsync(connection =>
            {
                var columns = connection.GetTableInfo("UserAccount");
                if (!columns.Any(column =>
                        string.Equals(column.Name, "AllergyDetailsJson", StringComparison.OrdinalIgnoreCase)))
                {
                    connection.Execute(
                        "ALTER TABLE UserAccount ADD COLUMN AllergyDetailsJson TEXT NOT NULL DEFAULT ''");
                }
            });
            await _db.RunInTransactionAsync(connection =>
            {
                var columns = connection.GetTableInfo("UserAccount");
                if (!columns.Any(column => column.Name.Equals("EmailVerified", StringComparison.OrdinalIgnoreCase)))
                    connection.Execute("ALTER TABLE UserAccount ADD COLUMN EmailVerified INTEGER NOT NULL DEFAULT 1");
                if (!columns.Any(column => column.Name.Equals("AccountStatus", StringComparison.OrdinalIgnoreCase)))
                    connection.Execute("ALTER TABLE UserAccount ADD COLUMN AccountStatus TEXT NOT NULL DEFAULT 'Active'");
                if (!columns.Any(column => column.Name.Equals("FacultyIdImagePath", StringComparison.OrdinalIgnoreCase)))
                    connection.Execute("ALTER TABLE UserAccount ADD COLUMN FacultyIdImagePath TEXT NOT NULL DEFAULT ''");

                // Previously self-registered Faculty accounts have not yet had
                // their employee ID checked by an authorized reviewer.
                connection.Execute("UPDATE UserAccount SET AccountStatus='PendingStaffIdReview' WHERE Role IN ('Faculty','Non-Teaching') AND (FacultyIdImagePath IS NULL OR FacultyIdImagePath='') AND AccountStatus='Active'");
                connection.Execute("UPDATE UserAccount SET AccountStatus='PendingStaffIdReview' WHERE Role IN ('Faculty','Non-Teaching') AND AccountStatus='PendingFacultyReview'");
            });
            await SeedTestAccountsAsync();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task SeedTestAccountsAsync() => await SeedAdminAsync();

    private async Task SeedAdminAsync()
    {
        var existing = await _db.Table<UserAccount>()
            .Where(a => a.Email == "clinicadmin@usjr.edu.ph")
            .FirstOrDefaultAsync();
        if (existing != null)
            return;

        await _db.InsertAsync(new UserAccount
        {
            FullName = "Clinic Administrator",
            Email = "clinicadmin@usjr.edu.ph",
            Password = PasswordSecurityService.HashPassword("ClinicAdmin123!"),
            Role = "Admin Personnel",
            IdNumber = "ADM-0001",
            ProgramOrDepartment = "USJ-R Clinic Administration",
            Position = "Clinic Account Reviewer",
            EmploymentStatus = "Active",
            EmailVerified = true,
            AccountStatus = "Active"
        });
    }

    public async Task<UserAccount?>
        GetAccountByIdNumberAsync(
            string idNumber)
    {
        await EnsureInitializedAsync();

        var normalized =
            idNumber.Trim();

        var all =
            await _db.Table<UserAccount>()
                .ToListAsync();

        return all.FirstOrDefault(a =>
            a.IdNumber.Equals(
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    public async Task<UserAccount?>
        GetAccountByEmailAsync(
            string email)
    {
        await EnsureInitializedAsync();

        var normalized =
            email.Trim();

        var all =
            await _db.Table<UserAccount>()
                .ToListAsync();

        return all.FirstOrDefault(a =>
            a.Email.Equals(
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> EmailExistsAsync(
        string email)
    {
        await EnsureInitializedAsync();

        var normalized =
            email.Trim()
                .ToLowerInvariant();

        var accounts =
            await _db.Table<UserAccount>()
                .ToListAsync();

        return accounts.Any(a =>
            a.Email.Trim()
                .ToLowerInvariant() ==
            normalized);
    }

    public async Task<bool> IdNumberExistsAsync(
        string idNumber)
    {
        await EnsureInitializedAsync();

        var normalized =
            idNumber.Trim()
                .ToLowerInvariant();

        var accounts =
            await _db.Table<UserAccount>()
                .ToListAsync();

        return accounts.Any(a =>
            a.IdNumber.Trim()
                .ToLowerInvariant() ==
            normalized);
    }

    public bool IsAllowedEmailDomain(
        string email)
    {
        var atIndex =
            email.LastIndexOf('@');

        if (atIndex < 0 ||
            atIndex == email.Length - 1)
        {
            return false;
        }

        var domain =
            email[(atIndex + 1)..]
                .Trim()
                .ToLowerInvariant();

        return AllowedEmailDomains.Contains(
            domain);
    }

    public async Task RegisterAsync(
        UserAccount account)
    {
        await EnsureInitializedAsync();

        if (!CentralPatientRoles.Contains(account.Role, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only Student, Faculty, and Non-Teaching accounts may self-register.");

        if ((account.Role.Equals("Faculty", StringComparison.OrdinalIgnoreCase) ||
             account.Role.Equals("Non-Teaching", StringComparison.OrdinalIgnoreCase)) &&
            (account.IdNumber.Length != 5 || account.IdNumber.Any(character => !char.IsDigit(character))))
            throw new InvalidOperationException("Faculty and Non-Teaching employee IDs must contain exactly five digits.");

        account.Email =
            account.Email.Trim().ToLowerInvariant();

        account.IdNumber =
            account.IdNumber.Trim();

        if (!PasswordSecurityService
            .IsHashedPassword(
                account.Password))
        {
            account.Password =
                PasswordSecurityService
                    .HashPassword(
                        account.Password);
        }

        account.EmailVerified = false;
        account.AccountStatus = "PendingEmailVerification";

        await _db.InsertAsync(account);
    }

    public async Task<bool> LoginAsync(
        string emailOrId,
        string password)
    {
        await EnsureInitializedAsync();

        var input =
            emailOrId.Trim()
                .ToLowerInvariant();

        var accounts =
            await _db.Table<UserAccount>()
                .ToListAsync();

        var account =
            accounts.FirstOrDefault(a =>
                a.Email.Trim()
                    .ToLowerInvariant() ==
                    input ||
                a.IdNumber.Trim()
                    .ToLowerInvariant() ==
                    input);

        if (account == null)
            return false;

        var passwordIsValid =
            false;

        if (PasswordSecurityService
            .IsHashedPassword(
                account.Password))
        {
            passwordIsValid =
                PasswordSecurityService
                    .VerifyPassword(
                        password,
                        account.Password);
        }
        else
        {
            // Compatibility for old accounts
            // created before password hashing.
            passwordIsValid =
                string.Equals(
                    account.Password,
                    password,
                    StringComparison.Ordinal);

            if (passwordIsValid)
            {
                account.Password =
                    PasswordSecurityService
                        .HashPassword(
                            password);

                await _db.UpdateAsync(
                    account);
            }
        }

        // Allow valid credentials for unverified/pending accounts to reach the
        // existing verification/review flow in LoginViewModel. Explicitly inactive
        // clinic employees are blocked here before any dashboard can open.
        if (!passwordIsValid || account.AccountStatus == "Inactive")
            return false;

        CurrentUser =
            account;

        return true;
    }

    public async Task<bool> UpdatePasswordAsync(
        int userId,
        string newPassword)
    {
        await EnsureInitializedAsync();

        var account =
            await _db.Table<UserAccount>()
                .Where(a =>
                    a.Id == userId)
                .FirstOrDefaultAsync();

        if (account == null)
            return false;

        account.Password =
            PasswordSecurityService
                .HashPassword(
                    newPassword);

        var updated =
            await _db.UpdateAsync(
                account);

        if (CurrentUser?.Id == userId)
            CurrentUser = account;

        return updated > 0;
    }

    public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        await EnsureInitializedAsync();
        var account = await GetAccountByDatabaseIdAsync(userId);
        if (account == null)
            return false;

        var valid = PasswordSecurityService.IsHashedPassword(account.Password)
            ? PasswordSecurityService.VerifyPassword(currentPassword, account.Password)
            : string.Equals(account.Password, currentPassword, StringComparison.Ordinal);
        if (!valid)
            return false;

        account.Password = PasswordSecurityService.HashPassword(newPassword);
        var updated = await _db.UpdateAsync(account);
        if (CurrentUser?.Id == userId)
            CurrentUser = account;
        return updated > 0;
    }

    public async Task<List<UserAccount>> GetPendingStaffIdAccountsAsync()
    {
        await EnsureInitializedAsync();
        if (!string.Equals(CurrentUser?.Role, "Admin Personnel", StringComparison.OrdinalIgnoreCase))
            return new List<UserAccount>();

        var accounts = await _db.Table<UserAccount>().ToListAsync();
        return accounts.Where(a => (a.Role == "Faculty" || a.Role == "Non-Teaching") &&
                a.EmailVerified && a.AccountStatus == "PendingStaffIdReview")
            .OrderBy(a => a.FullName).ToList();
    }

    public async Task<bool> ReviewStaffIdAccountAsync(int accountId, bool approve)
    {
        await EnsureInitializedAsync();
        if (!string.Equals(CurrentUser?.Role, "Admin Personnel", StringComparison.OrdinalIgnoreCase))
            return false;

        var account = await GetAccountByDatabaseIdAsync(accountId);
        if (account == null || (account.Role != "Faculty" && account.Role != "Non-Teaching") ||
            !account.EmailVerified || account.AccountStatus != "PendingStaffIdReview")
            return false;

        if (approve && (string.IsNullOrWhiteSpace(account.FacultyIdImagePath) || !File.Exists(account.FacultyIdImagePath)))
            return false;

        account.AccountStatus = approve ? "Active" : "StaffIdReviewRejected";
        return await _db.UpdateAsync(account) > 0;
    }

    public async Task<bool> UpdatePendingStaffIdImageAsync(int accountId, string imagePath)
    {
        await EnsureInitializedAsync();
        var account = await GetAccountByDatabaseIdAsync(accountId);
        if (account == null || (account.Role != "Faculty" && account.Role != "Non-Teaching") ||
            !account.EmailVerified || account.AccountStatus != "PendingStaffIdReview" || string.IsNullOrWhiteSpace(imagePath))
            return false;
        account.FacultyIdImagePath = imagePath;
        return await _db.UpdateAsync(account) > 0;
    }

    public async Task<List<UserAccount>>
        GetAllPatientsAsync()
    {
        await EnsureInitializedAsync();

        var all = await _db.Table<UserAccount>().ToListAsync();

        return all
            .Where(account => CentralPatientRoles.Contains(
                account.Role,
                StringComparer.OrdinalIgnoreCase))
            .OrderBy(account => account.FullName)
            .ToList();
    }

    public async Task<List<UserAccount>> GetClinicStaffAsync(
        string? roleFilter = null,
        bool includeInactive = true)
    {
        await EnsureInitializedAsync();
        if (!IsClinicAdministrator(CurrentUser))
            return new List<UserAccount>();

        var accounts = await _db.Table<UserAccount>().ToListAsync();
        return accounts
            .Where(account => IsClinicStaffRole(account.Role))
            .Where(account => string.IsNullOrWhiteSpace(roleFilter) ||
                              string.Equals(account.Role, roleFilter, StringComparison.OrdinalIgnoreCase))
            .Where(account => includeInactive || IsActiveClinicStaff(account))
            .OrderBy(account => account.Role)
            .ThenBy(account => account.FullName)
            .ToList();
    }

    public async Task<bool> CreateClinicStaffAsync(UserAccount account, string initialPassword)
    {
        await EnsureInitializedAsync();
        if (!IsClinicAdministrator(CurrentUser) || !IsClinicStaffRole(account.Role))
            return false;

        account.FullName = account.FullName.Trim();
        account.Email = account.Email.Trim().ToLowerInvariant();
        account.IdNumber = account.IdNumber.Trim();
        if (string.IsNullOrWhiteSpace(account.FullName) ||
            string.IsNullOrWhiteSpace(account.Email) ||
            string.IsNullOrWhiteSpace(account.IdNumber) ||
            string.IsNullOrWhiteSpace(initialPassword))
            return false;

        var all = await _db.Table<UserAccount>().ToListAsync();
        if (all.Any(existing =>
                existing.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase) ||
                existing.IdNumber.Equals(account.IdNumber, StringComparison.OrdinalIgnoreCase)))
            return false;

        account.Password = PasswordSecurityService.HashPassword(initialPassword);
        account.EmailVerified = true;
        account.AccountStatus = "Active";
        account.EmploymentStatus = "Active";
        await _db.InsertAsync(account);
        return true;
    }

    public async Task<bool> UpdateClinicStaffAsync(UserAccount editedAccount)
    {
        await EnsureInitializedAsync();
        if (!IsClinicAdministrator(CurrentUser) || !IsClinicStaffRole(editedAccount.Role))
            return false;

        var existing = await GetAccountByDatabaseIdAsync(editedAccount.Id);
        if (existing == null || !IsClinicStaffRole(existing.Role))
            return false;

        editedAccount.FullName = editedAccount.FullName.Trim();
        editedAccount.Email = editedAccount.Email.Trim().ToLowerInvariant();
        editedAccount.IdNumber = editedAccount.IdNumber.Trim();
        if (string.IsNullOrWhiteSpace(editedAccount.FullName) ||
            string.IsNullOrWhiteSpace(editedAccount.Email) ||
            string.IsNullOrWhiteSpace(editedAccount.IdNumber))
            return false;

        var all = await _db.Table<UserAccount>().ToListAsync();
        if (all.Any(other => other.Id != editedAccount.Id &&
                (other.Email.Equals(editedAccount.Email, StringComparison.OrdinalIgnoreCase) ||
                 other.IdNumber.Equals(editedAccount.IdNumber, StringComparison.OrdinalIgnoreCase))))
            return false;

        var roleChanged = !string.Equals(existing.Role, editedAccount.Role, StringComparison.OrdinalIgnoreCase);
        editedAccount.Password = existing.Password;
        editedAccount.EmailVerified = existing.EmailVerified;
        editedAccount.AccountStatus = existing.AccountStatus;
        editedAccount.EmploymentStatus = existing.EmploymentStatus;
        editedAccount.FacultyIdImagePath = existing.FacultyIdImagePath;
        editedAccount.Allergies = existing.Allergies;
        editedAccount.AllergyDetailsJson = existing.AllergyDetailsJson;
        var updated = await _db.UpdateAsync(editedAccount);
        if (updated > 0 && CurrentUser?.Id == editedAccount.Id)
            CurrentUser = editedAccount;
        if (updated > 0 && roleChanged)
            await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(editedAccount.Id);
        return updated > 0;
    }

    public async Task<bool> SetClinicStaffActiveAsync(int accountId, bool isActive)
    {
        await EnsureInitializedAsync();
        if (!IsClinicAdministrator(CurrentUser))
            return false;

        var account = await GetAccountByDatabaseIdAsync(accountId);
        if (account == null || !IsClinicStaffRole(account.Role))
            return false;

        account.AccountStatus = isActive ? "Active" : "Inactive";
        account.EmploymentStatus = isActive ? "Active" : "Inactive";
        if (isActive)
            account.EmailVerified = true;
        var updated = await _db.UpdateAsync(account);
        if (updated > 0 && !isActive)
            await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(account.Id);
        return updated > 0;
    }

    public static bool IsActiveClinicStaff(UserAccount account) =>
        IsClinicStaffRole(account.Role) &&
        account.EmailVerified &&
        string.Equals(account.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(account.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase);

    public async Task<List<UserAccount>> GetActiveClinicStaffByRoleAsync(string role)
    {
        await EnsureInitializedAsync();
        if (!IsClinicStaffRole(role))
            return new List<UserAccount>();

        return (await _db.Table<UserAccount>().ToListAsync())
            .Where(account => string.Equals(account.Role, role, StringComparison.OrdinalIgnoreCase) &&
                              IsActiveClinicStaff(account))
            .OrderBy(account => account.FullName)
            .ToList();
    }

    public async Task<List<UserAccount>>
        GetAllDoctorsAsync()
    {
        return await GetActiveClinicStaffByRoleAsync("Doctor");
    }

    public async Task<List<UserAccount>> GetAllDentistsAsync() =>
        await GetActiveClinicStaffByRoleAsync("Dentist");

    public async Task<bool> UpdateProfileAsync(
        UserAccount updatedAccount)
    {
        await EnsureInitializedAsync();

        // Profile editors update only user-facing fields. Preserve verification
        // and reviewer data so a profile save cannot reset account status.
        var existingAccount = await _db.Table<UserAccount>()
            .Where(account => account.Id == updatedAccount.Id)
            .FirstOrDefaultAsync();
        if (existingAccount != null)
        {
            updatedAccount.EmailVerified = existingAccount.EmailVerified;
            updatedAccount.AccountStatus = existingAccount.AccountStatus;
            updatedAccount.FacultyIdImagePath = existingAccount.FacultyIdImagePath;
            if (string.IsNullOrWhiteSpace(updatedAccount.AllergyDetailsJson))
                updatedAccount.AllergyDetailsJson = existingAccount.AllergyDetailsJson;
        }
        var result =
            await _db.UpdateAsync(
                updatedAccount);

        if (CurrentUser != null &&
            CurrentUser.Id ==
                updatedAccount.Id)
        {
            CurrentUser =
                updatedAccount;
        }

        return result > 0;
    }

    public async Task<UserAccount?>
        GetAccountByIdentifierAsync(
            string emailOrId)
    {
        await EnsureInitializedAsync();

        var identifier =
            emailOrId?.Trim() ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
            identifier))
        {
            return null;
        }

        var normalizedEmail =
            identifier.ToLowerInvariant();

        var normalizedId =
            identifier.ToUpperInvariant();

        return await _db.Table<UserAccount>()
            .Where(user =>
                user.Email ==
                    normalizedEmail ||
                user.IdNumber ==
                    normalizedId)
            .FirstOrDefaultAsync();
    }

    public void Logout()
    {
        CurrentUser =
            null;
    }

    public async Task<UserAccount?>
        GetAccountByDatabaseIdAsync(
            int userId)
    {
        await EnsureInitializedAsync();

        return await _db.Table<UserAccount>()
            .Where(a =>
                a.Id == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> RestoreSessionAsync(
        int userId)
    {
        await EnsureInitializedAsync();

        var account =
            await _db.Table<UserAccount>()
                .Where(a =>
                    a.Id == userId)
                .FirstOrDefaultAsync();

        if (account == null || !account.EmailVerified || account.AccountStatus != "Active")
            return false;

        CurrentUser =
            account;

        return true;
    }
}



