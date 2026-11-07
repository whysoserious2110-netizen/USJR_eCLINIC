using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class ApeLabResultService
{
    public static ApeLabResultService Instance { get; } = new ApeLabResultService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private ApeLabResultService()
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
            await _db.CreateTableAsync<ApeLabResult>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task AddAsync(ApeLabResult result)
    {
        var nurse = AuthService.Instance.CurrentUser;
        if (nurse?.Role != "Nurse")
            throw new UnauthorizedAccessException("Only a Nurse can encode APE laboratory results.");

        if (string.IsNullOrWhiteSpace(result.PatientEmail) ||
            !new[] { result.Cbc, result.Urinalysis, result.ChestXray, result.Ecg, result.DrugTest }
                .Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            throw new ArgumentException("Select a patient and enter at least one verified test result.");
        }

        var patient = await AuthService.Instance.GetAccountByEmailAsync(result.PatientEmail);
        if (patient == null || patient.Role is not ("Faculty" or "Admin Personnel" or "Non-Teaching"))
            throw new ArgumentException("APE results can only be encoded for eligible Faculty, Admin Personnel, or Non-Teaching accounts.");

        result.PatientEmail = patient.Email;
        result.EncodedBy = nurse.FullName;
        result.DateEncoded = DateTime.Now;

        await EnsureInitializedAsync();
        await _db.InsertAsync(result);
    }

    public async Task<List<ApeLabResult>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<ApeLabResult>().ToListAsync();
        return all.Where(r => r.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(r => r.DateEncoded)
                   .ToList();
    }
}
