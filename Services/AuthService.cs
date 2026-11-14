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

    private async Task SeedTestAccountsAsync()
    {
        await SeedDoctorAsync();
        await SeedNurseAsync();
        await SeedDentistAsync();
        await SeedReadsAsync();
        await SeedAdminAsync();
    }

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

    private async Task SeedDoctorAsync()
    {
        var existing =
            await _db.Table<UserAccount>()
                .Where(a =>
                    a.Email == "doctor@usjr.edu.ph")
                .FirstOrDefaultAsync();

        if (existing != null)
            return;

        await _db.InsertAsync(new UserAccount
        {
            FullName = "Dr. Jose Rizal",
            Email = "doctor@usjr.edu.ph",

            Password =
                PasswordSecurityService.HashPassword(
                    "Doctor123!"),

            Role = "Doctor",
            IdNumber = "DOC-0001",
            ProgramOrDepartment = "USJ-R Clinic",
            Specialization = "General Medicine",
            LicenseNumber = "PRC-0123456",
            YearsOfExperience = "8",
            Position = "University Physician",
            ConsultationSchedule = "Monday – Friday",
            EmploymentStatus = "Active"
        });
    }

    private async Task SeedNurseAsync()
    {
        var existing =
            await _db.Table<UserAccount>()
                .Where(a =>
                    a.Email == "nurse@usjr.edu.ph")
                .FirstOrDefaultAsync();

        if (existing != null)
            return;

        await _db.InsertAsync(new UserAccount
        {
            FullName = "Ana Reyes",
            Email = "nurse@usjr.edu.ph",

            Password =
                PasswordSecurityService.HashPassword(
                    "Nurse123!"),

            Role = "Nurse",
            IdNumber = "NUR-0001",
            ProgramOrDepartment = "USJ-R Clinic",
            Position = "Staff Nurse",
            ConsultationSchedule = "Monday – Saturday",
            EmploymentStatus = "Active"
        });
    }

    private async Task SeedDentistAsync()
    {
        var existing =
            await _db.Table<UserAccount>()
                .Where(a =>
                    a.Email == "dentist@usjr.edu.ph")
                .FirstOrDefaultAsync();

        if (existing != null)
            return;

        await _db.InsertAsync(new UserAccount
        {
            FullName = "Dr. Maria Santos",
            Email = "dentist@usjr.edu.ph",

            Password =
                PasswordSecurityService.HashPassword(
                    "Dentist123!"),

            Role = "Dentist",
            IdNumber = "DEN-0001",
            ProgramOrDepartment =
                "USJ-R Clinic - Dental",
            Specialization = "General Dentistry",
            LicenseNumber = "PRC-0567890",
            YearsOfExperience = "6",
            Position = "University Dentist",
            ConsultationSchedule =
                "Mon, Wed, Fri, Sat",
            EmploymentStatus = "Active"
        });
    }

    private async Task SeedReadsAsync()
    {
        var existing =
            await _db.Table<UserAccount>()
                .Where(a =>
                    a.Email == "reads@usjr.edu.ph")
                .FirstOrDefaultAsync();

        if (existing != null)
            return;

        await _db.InsertAsync(new UserAccount
        {
            FullName = "Carlos Mendoza",
            Email = "reads@usjr.edu.ph",

            Password =
                PasswordSecurityService.HashPassword(
                    "Reads123!"),

            Role = "R.E.A.D.S. Scholar",
            IdNumber = "READS-0001",
            ProgramOrDepartment =
                "USJ-R Clinic - Front Desk",
            Position = "Front Desk Coordinator",
            EmploymentStatus = "Active"
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

        if (!passwordIsValid)
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

    public async Task<List<UserAccount>>
        GetAllDoctorsAsync()
    {
        await EnsureInitializedAsync();

        var all =
            await _db.Table<UserAccount>()
                .ToListAsync();

        return all
            .Where(a =>
                a.Role == "Doctor")
            .ToList();
    }

    public async Task<List<UserAccount>> GetAllDentistsAsync()
    {
        await EnsureInitializedAsync();

        var all = await _db.Table<UserAccount>().ToListAsync();

        return all
            .Where(account => account.Role == "Dentist")
            .ToList();
    }

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



