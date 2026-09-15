using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class PrescriptionService
{
    public static PrescriptionService Instance { get; } = new PrescriptionService();

    private readonly SQLiteAsyncConnection _db;

    private PrescriptionService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Prescription>().Wait();
    }

    public async Task<List<Prescription>> GetForPatientAsync(string patientEmail)
    {
        var all = await _db.Table<Prescription>().ToListAsync();
        return all
            .Where(p => p.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.DatePrescribed)
            .ToList();
    }


    public async Task<List<Prescription>> GetClinicGivenUndispensedAsync()
    {
        var all = await _db.Table<Prescription>().ToListAsync();
        return all.Where(p => p.IsClinicGiven && !p.IsDispensed).OrderByDescending(p => p.DatePrescribed).ToList();
    }

    public async Task<bool> MarkDispensedAsync(int prescriptionId)
    {
        var rx = await _db.Table<Prescription>().Where(p => p.Id == prescriptionId).FirstOrDefaultAsync();
        if (rx == null) return false;

        rx.IsDispensed = true;
        rx.DispensedDate = DateTime.Now;
        await _db.UpdateAsync(rx);

        await NotificationService.Instance.AddAsync(
            rx.PatientEmail,
            "Medication Dispensed",
            $"Your {rx.MedicineName} ({rx.Dosage}) has been dispensed at the clinic.");

        return true;
    }


    // TODO: called by Doctor's Create Prescription screen once that's built
    public async Task AddAsync(Prescription prescription) => await _db.InsertAsync(prescription);
}