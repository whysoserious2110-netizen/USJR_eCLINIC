using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AuthService
{
    public static AuthService Instance { get; } = new AuthService();

    public UserAccount? CurrentUser { get; private set; }

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly string[] AllowedEmailDomains = { "usjr.edu.ph", "gmail.com" };

    private AuthService()
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

            await _db.CreateTableAsync<UserAccount>();
            await SeedTestAccounts();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task SeedTestAccounts()
    {
        var doctorExists = await _db.Table<UserAccount>()
            .Where(a => a.Email == "doctor@usjr.edu.ph")
            .FirstOrDefaultAsync();

        if (doctorExists == null)
        {
            await _db.InsertAsync(new UserAccount
            {
                FullName = "Dr. Jose Rizal",
                Email = "doctor@usjr.edu.ph",
                Password = "Doctor123!",
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



        var nurseExists = await _db.Table<UserAccount>()
    .Where(a => a.Email == "nurse@usjr.edu.ph")
    .FirstOrDefaultAsync();

        if (nurseExists == null)
        {
            await _db.InsertAsync(new UserAccount
            {
                FullName = "Ana Reyes",
                Email = "nurse@usjr.edu.ph",
                Password = "Nurse123!",
                Role = "Nurse",
                IdNumber = "NUR-0001",
                ProgramOrDepartment = "USJ-R Clinic",
                Position = "Staff Nurse",
                ConsultationSchedule = "Monday – Saturday",
                EmploymentStatus = "Active"
            });
        }


    }


    public async Task<UserAccount?> GetAccountByIdNumberAsync(string idNumber)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<UserAccount>().ToListAsync();
        return all.FirstOrDefault(a => a.IdNumber.Equals(idNumber.Trim(), StringComparison.OrdinalIgnoreCase));
    }




    public async Task<bool> EmailExistsAsync(string email)
    {
        await EnsureInitializedAsync();
        var normalized = email.Trim().ToLowerInvariant();
        var accounts = await _db.Table<UserAccount>().ToListAsync();
        return accounts.Any(a => a.Email.Trim().ToLowerInvariant() == normalized);
    }

    public async Task<bool> IdNumberExistsAsync(string idNumber)
    {
        await EnsureInitializedAsync();
        var normalized = idNumber.Trim().ToLowerInvariant();
        var accounts = await _db.Table<UserAccount>().ToListAsync();
        return accounts.Any(a => a.IdNumber.Trim().ToLowerInvariant() == normalized);
    }

    public bool IsAllowedEmailDomain(string email)
    {
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1) return false;

        var domain = email[(atIndex + 1)..].Trim().ToLowerInvariant();
        return AllowedEmailDomains.Contains(domain);
    }

    public async Task RegisterAsync(UserAccount account)
    {
        await EnsureInitializedAsync();
        account.Email = account.Email.Trim();
        account.IdNumber = account.IdNumber.Trim();
        await _db.InsertAsync(account);
    }

    public async Task<bool> LoginAsync(string emailOrId, string password)
    {
        await EnsureInitializedAsync();

        var input = emailOrId.Trim().ToLowerInvariant();
        var pass = password.Trim();

        var accounts = await _db.Table<UserAccount>().ToListAsync();

        var match = accounts.FirstOrDefault(a =>
            (a.Email.Trim().ToLowerInvariant() == input ||
             a.IdNumber.Trim().ToLowerInvariant() == input) &&
            a.Password == pass);

        if (match == null)
            return false;

        CurrentUser = match;
        return true;
    }

    public async Task<UserAccount?> GetAccountByEmailAsync(string email)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<UserAccount>().ToListAsync();
        return all.FirstOrDefault(a => a.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<UserAccount>> GetAllPatientsAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<UserAccount>().ToListAsync();
        var staffRoles = new[] { "Doctor", "Nurse", "Dentist" };
        return all.Where(a => !staffRoles.Contains(a.Role)).OrderBy(a => a.FullName).ToList();
    }

    public async Task<List<UserAccount>> GetAllDoctorsAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<UserAccount>().ToListAsync();
        return all.Where(a => a.Role == "Doctor").ToList();
    }



    public async Task<bool> UpdateProfileAsync(UserAccount updatedAccount)
    {
        await EnsureInitializedAsync();
        var result = await _db.UpdateAsync(updatedAccount);

        if (CurrentUser != null && CurrentUser.Id == updatedAccount.Id)
            CurrentUser = updatedAccount;

        return result > 0;
    }

    public void Logout() => CurrentUser = null;
}