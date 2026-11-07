using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class VitalSignsService
{
    public static VitalSignsService Instance { get; } = new VitalSignsService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private VitalSignsService()
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
            await _db.CreateTableAsync<VitalSigns>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<bool> SaveForCheckedInAppointmentAsync(VitalSigns vitals)
    {
        var nurse = AuthService.Instance.CurrentUser;
        if (nurse?.Role != "Nurse" ||
            vitals.AppointmentId <= 0 ||
            string.IsNullOrWhiteSpace(vitals.PatientEmail))
        {
            return false;
        }

        await EnsureInitializedAsync();
        // Ensure the appointment table exists before using the shared SQLite transaction.
        await AppointmentService.Instance.GetByIdAsync(vitals.AppointmentId);

        var saved = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(vitals.AppointmentId);
            if (appointment == null ||
                appointment.VisitDate.Date != DateTime.Today ||
                appointment.Status is not ("CheckedIn" or "VitalsRecorded") ||
                !appointment.PatientEmail.Equals(vitals.PatientEmail, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            vitals.PatientEmail = appointment.PatientEmail;
            vitals.RecordedBy = nurse.FullName;
            vitals.DateRecorded = DateTime.Now;
            connection.Insert(vitals);

            if (appointment.Status != "VitalsRecorded")
            {
                appointment.Status = "VitalsRecorded";
                connection.Update(appointment);
            }

            saved = true;
        });

        if (saved)
        {
            try
            {
                var appointment = await AppointmentService.Instance.GetByIdAsync(vitals.AppointmentId);
                if (appointment != null)
                {
                    var clinic = appointment.ServiceType == "Dental" ? "Dental" : "Medical";
                    var title = clinic == "Dental" ? "Dental Visit Ready for Dentist" : "Medical Visit Ready for Doctor";
                    await AppointmentService.Instance.NotifyAssignedClinicalStaffAsync(
                        appointment,
                        title,
                        $"Nurse triage and vital signs are complete for {appointment.PatientEmail}'s {clinic.ToLowerInvariant()} appointment. The visit is ready for the clinician.");
                }
            }
            catch
            {
                // Saved vital signs remain successful even if a notification cannot be created.
            }
        }

        return saved;
    }

    public async Task AddAsync(VitalSigns vitals)
    {
        var saved = await SaveForCheckedInAppointmentAsync(vitals);
        if (!saved)
            throw new InvalidOperationException("Vitals can only be saved by a Nurse for a checked-in appointment scheduled today.");
    }

    public async Task<List<VitalSigns>> GetForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<VitalSigns>().ToListAsync();
        return all.Where(v => v.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
                   .OrderByDescending(v => v.DateRecorded)
                 .ToList();
    }

    public async Task<VitalSigns?> GetForAppointmentAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        if (appointmentId <= 0)
            return null;

        var records = await _db.Table<VitalSigns>()
            .Where(v => v.AppointmentId == appointmentId)
            .ToListAsync();

        return records
            .OrderByDescending(v => v.DateRecorded)
            .FirstOrDefault();
    }

}
