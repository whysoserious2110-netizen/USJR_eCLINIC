using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AuthService
{
    public static AuthService Instance { get; } =
        new AuthService();

    public UserAccount? CurrentUser { get; private set; }

    // Roles that self-register and authenticate through the
    // central API (see PatientRolePolicy on the API side).
    // Everything else (Doctor, Nurse, Dentist, R.E.A.D.S.
    // Scholar) is a locally seeded/provisioned account that
    // signs in through the local password instead.
    public static readonly string[] CentralPatientRoles =
    {
        "Student",
        "Faculty",
        "Admin Personnel",
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

            await _db.CreateTableAsync<UserAccount>();
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
            ProgramOrDepartment = "USJ-R Clinic - Front Desk",
            Position = "Front Desk Coordinator",
            EmploymentStatus = "Active"
        });
    }



    public async Task<UserAccount?>
        GetAccountByIdNumberAsync(string idNumber)
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
        GetAccountByEmailAsync(string email)
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
            email.Trim().ToLowerInvariant();

        var accounts =
            await _db.Table<UserAccount>()
                .ToListAsync();

        return accounts.Any(a =>
            a.Email.Trim().ToLowerInvariant() ==
            normalized);
    }

    public async Task<bool> IdNumberExistsAsync(
        string idNumber)
    {
        await EnsureInitializedAsync();

        var normalized =
            idNumber.Trim().ToLowerInvariant();

        var accounts =
            await _db.Table<UserAccount>()
                .ToListAsync();

        return accounts.Any(a =>
            a.IdNumber.Trim().ToLowerInvariant() ==
            normalized);
    }

    public bool IsAllowedEmailDomain(string email)
    {
        var atIndex = email.LastIndexOf('@');

        if (atIndex < 0 ||
            atIndex == email.Length - 1)
        {
            return false;
        }

        var domain = email[(atIndex + 1)..]
            .Trim()
            .ToLowerInvariant();

        return AllowedEmailDomains.Contains(domain);
    }

    public async Task RegisterAsync(
        UserAccount account)
    {
        await EnsureInitializedAsync();

        account.Email = account.Email.Trim();
        account.IdNumber = account.IdNumber.Trim();

        if (!PasswordSecurityService.IsHashedPassword(
            account.Password))
        {
            account.Password =
                PasswordSecurityService.HashPassword(
                    account.Password);
        }

        await _db.InsertAsync(account);
    }

    public async Task<bool> LoginAsync(
        string emailOrId,
        string password)
    {
        await EnsureInitializedAsync();

        var input =
            emailOrId.Trim().ToLowerInvariant();

        var accounts =
            await _db.Table<UserAccount>()
                .ToListAsync();

        var account = accounts.FirstOrDefault(a =>
            a.Email.Trim().ToLowerInvariant() == input ||
            a.IdNumber.Trim().ToLowerInvariant() == input);

        if (account == null)
            return false;

        var passwordIsValid = false;

        if (PasswordSecurityService.IsHashedPassword(
            account.Password))
        {
            passwordIsValid =
                PasswordSecurityService.VerifyPassword(
                    password,
                    account.Password);
        }
        else
        {
            // Temporary compatibility for accounts that
            // were created before password hashing.
            passwordIsValid =
                string.Equals(
                    account.Password,
                    password,
                    StringComparison.Ordinal);

            if (passwordIsValid)
            {
                // Automatically replace the old plaintext
                // password after a successful login.
                account.Password =
                    PasswordSecurityService.HashPassword(
                        password);

                await _db.UpdateAsync(account);
            }
        }

        if (!passwordIsValid)
            return false;

        CurrentUser = account;

        return true;
    }


    public async Task<bool> UpdatePasswordAsync(
    int userId,
    string newPassword)
    {
        await EnsureInitializedAsync();

        var account =
            await _db.Table<UserAccount>()
                .Where(a => a.Id == userId)
                .FirstOrDefaultAsync();

        if (account == null)
            return false;

        account.Password =
            PasswordSecurityService.HashPassword(
                newPassword);

        var updated =
            await _db.UpdateAsync(account);

        if (CurrentUser?.Id == userId)
            CurrentUser = account;

        return updated > 0;
    }




    public async Task<List<UserAccount>>
        GetAllPatientsAsync()
    {
        await EnsureInitializedAsync();

        var all =
            await _db.Table<UserAccount>()
                .ToListAsync();

        var staffRoles = new[]
        {
            "Doctor",
            "Nurse",
            "Dentist"
        };

        return all
            .Where(a => !staffRoles.Contains(a.Role))
            .OrderBy(a => a.FullName)
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
            .Where(a => a.Role == "Doctor")
            .ToList();
    }

    public async Task<bool> UpdateProfileAsync(
        UserAccount updatedAccount)
    {
        await EnsureInitializedAsync();

        var result =
            await _db.UpdateAsync(updatedAccount);

        if (CurrentUser != null &&
            CurrentUser.Id == updatedAccount.Id)
        {
            CurrentUser = updatedAccount;
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

        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var normalizedEmail =
            identifier.ToLowerInvariant();

        var normalizedId =
            identifier.ToUpperInvariant();

        return await _db.Table<UserAccount>()
            .Where(user =>
                user.Email == normalizedEmail ||
                user.IdNumber == normalizedId)
            .FirstOrDefaultAsync();
    }

    public async Task<UserAccount>
        SignInCentralStudentAsync(
            ApiStudentUser apiStudent)
    {
        await EnsureInitializedAsync();

        var normalizedStudentId =
            apiStudent.StudentId
                .Trim()
                .ToUpperInvariant();

        var normalizedEmail =
            apiStudent.Email
                .Trim()
                .ToLowerInvariant();

        var localAccount =
            await _db.Table<UserAccount>()
                .Where(user =>
                    user.IdNumber ==
                        normalizedStudentId ||
                    user.Email ==
                        normalizedEmail)
                .FirstOrDefaultAsync();

        if (localAccount == null)
        {
            localAccount = new UserAccount
            {
                FullName =
                    apiStudent.FullName,

                IdNumber =
                    normalizedStudentId,

                Email =
                    normalizedEmail,

                // Trust whatever role the central API
                // authenticated this account as (Student,
                // Faculty, Admin Personnel, or Non-Teaching).
                Role =
                    apiStudent.Role,

                // No usable central password is stored locally.
                // This is a hash of a random unknown value.
                Password =
                    PasswordSecurityService.HashPassword(
                        $"{Guid.NewGuid():N}" +
                        $"{Guid.NewGuid():N}")
            };

            await _db.InsertAsync(localAccount);
        }
        else
        {
            localAccount.FullName =
                apiStudent.FullName;

            localAccount.IdNumber =
                normalizedStudentId;

            localAccount.Email =
                normalizedEmail;

            localAccount.Role =
                apiStudent.Role;

            // Remove the ability to authenticate this Student
            // using an old locally stored password.
            localAccount.Password =
                PasswordSecurityService.HashPassword(
                    $"{Guid.NewGuid():N}" +
                    $"{Guid.NewGuid():N}");

            await _db.UpdateAsync(localAccount);
        }

        CurrentUser = localAccount;

        return localAccount;
    }




    public void Logout()
    {
        CurrentUser = null;
    }



    public async Task<UserAccount?>
    GetAccountByDatabaseIdAsync(int userId)
    {
        await EnsureInitializedAsync();

        return await _db.Table<UserAccount>()
            .Where(a => a.Id == userId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> RestoreSessionAsync(
    int userId)
    {
        await EnsureInitializedAsync();

        var account =
            await _db.Table<UserAccount>()
                .Where(a => a.Id == userId)
                .FirstOrDefaultAsync();

        if (account == null)
            return false;

        CurrentUser = account;

        return true;
    }



}