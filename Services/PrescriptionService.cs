using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public sealed record DispenseResult(bool Success, string Message);

public class PrescriptionService
{
    public static PrescriptionService Instance { get; } = new();

    private readonly SQLiteAsyncConnection _db = ClinicDatabase.Connection;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private PrescriptionService()
    {
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

            await _db.CreateTableAsync<Prescription>();

            await _db.RunInTransactionAsync(connection =>
            {
                var columns = connection.GetTableInfo("Prescription");
                if (!columns.Any(column =>
                        string.Equals(column.Name, "Quantity", StringComparison.OrdinalIgnoreCase)))
                {
                    connection.Execute(
                        "ALTER TABLE Prescription ADD COLUMN Quantity INTEGER NOT NULL DEFAULT 1");
                }

                connection.Execute(
                    "UPDATE Prescription SET Quantity = 1 WHERE Quantity <= 0");
            });

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<Prescription>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Prescription>().ToListAsync();

        return all
            .Where(prescription =>
                prescription.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(prescription => prescription.DatePrescribed)
            .ToList();
    }

    public async Task<List<Prescription>> GetClinicGivenUndispensedAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Prescription>().ToListAsync();

        return all
            .Where(prescription => prescription.IsClinicGiven && !prescription.IsDispensed)
            .OrderByDescending(prescription => prescription.DatePrescribed)
            .ToList();
    }

    public async Task<DispenseResult> MarkDispensedAsync(int prescriptionId)
    {
        await EnsureInitializedAsync();
        await MedicineStockService.Instance.GetAllAsync();

        var message = "The prescription could not be dispensed.";
        Prescription? dispensedPrescription = null;

        await _db.RunInTransactionAsync(connection =>
        {
            var prescription = connection.Find<Prescription>(prescriptionId);
            if (prescription == null)
            {
                message = "This prescription could not be found.";
                return;
            }

            if (!prescription.IsClinicGiven)
            {
                message = "Only clinic-given prescriptions can be dispensed here.";
                return;
            }

            if (prescription.IsDispensed)
            {
                message = "This prescription has already been dispensed.";
                return;
            }

            var medicineName = prescription.MedicineName.Trim();
            var stock = connection.Table<MedicineStock>()
                .ToList()
                .FirstOrDefault(item => string.Equals(
                    item.MedicineName.Trim(),
                    medicineName,
                    StringComparison.OrdinalIgnoreCase));

            if (stock == null)
            {
                message = $"“{medicineName}” is not in inventory. Add it to inventory before dispensing.";
                return;
            }

            var quantity = prescription.Quantity > 0 ? prescription.Quantity : 1;
            if (stock.Quantity < quantity)
            {
                message = $"Not enough {stock.MedicineName} in stock. Available: {stock.Quantity}; required: {quantity}.";
                return;
            }

            stock.Quantity -= quantity;
            stock.LastUpdated = DateTime.Now;
            prescription.Quantity = quantity;
            prescription.IsDispensed = true;
            prescription.DispensedDate = DateTime.Now;

            connection.Update(stock);
            connection.Update(prescription);

            dispensedPrescription = prescription;
            message = "Prescription dispensed and inventory updated.";
        });

        if (dispensedPrescription == null)
            return new(false, message);

        try
        {
            await NotificationService.Instance.AddAsync(
                dispensedPrescription.PatientEmail,
                "Medication Dispensed",
                $"Your {dispensedPrescription.MedicineName} ({dispensedPrescription.Dosage}) has been dispensed at the clinic.");
        }
        catch
        {
            return new(true, "Prescription dispensed and inventory updated. The patient notification could not be saved.");
        }

        return new(true, message);
    }

    public async Task AddAsync(Prescription prescription)
    {
        if (string.IsNullOrWhiteSpace(prescription.PatientEmail) ||
            string.IsNullOrWhiteSpace(prescription.MedicineName) ||
            string.IsNullOrWhiteSpace(prescription.Dosage) ||
            prescription.Quantity <= 0)
        {
            throw new ArgumentException("A prescription needs a patient, medicine, dosage, and positive quantity.");
        }

        await EnsureInitializedAsync();
        prescription.MedicineName = prescription.MedicineName.Trim();
        prescription.Dosage = prescription.Dosage.Trim();
        await _db.InsertAsync(prescription);
    }
}
