using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AppointmentService
{
    public static AppointmentService Instance { get; } = new AppointmentService();

    private readonly SQLiteAsyncConnection _db;
    private bool _initialized = false;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private AppointmentService()
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
            await _db.CreateTableAsync<Appointment>();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task BookAsync(Appointment appointment)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(appointment);

        await NotificationService.Instance.AddAsync(
            appointment.PatientEmail,
            "Appointment Requested",
            $"Your {appointment.ServiceType} appointment ({appointment.SubService}) on {appointment.VisitDate:MMM dd, yyyy} at {appointment.VisitTime} has been submitted and is Pending.");

        if (appointment.ServiceType == "Medical")
        {
            var doctors = await AuthService.Instance.GetAllDoctorsAsync();
            foreach (var doc in doctors)
            {
                await NotificationService.Instance.AddAsync(
                    doc.Email,
                    "New Appointment Request",
                    $"A new Medical appointment ({appointment.SubService}) was booked for {appointment.VisitDate:MMM dd, yyyy} at {appointment.VisitTime}.");
            }
        }
    }


    public async Task<List<Appointment>> GetCertRequestsAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.ServiceType == "Cert Request" && (a.Status == "Pending" || a.Status == "Confirmed"))
            .OrderBy(a => a.VisitDate)
            .ToList();
    }

    public async Task<bool> IssueCertificateAsync(int appointmentId, string content)
    {
        await EnsureInitializedAsync();
        var appt = await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();
        if (appt == null) return false;

        appt.CertificateContent = content;
        appt.IssuedDate = DateTime.Now;
        appt.Status = "Completed";
        await _db.UpdateAsync(appt);
        return true;
    }


    public async Task<bool> CancelAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        var appt = await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();
        if (appt == null) return false;

        appt.Status = "Cancelled";
        await _db.UpdateAsync(appt);

        await NotificationService.Instance.AddAsync(
            appt.PatientEmail,
            "Appointment Cancelled",
            $"Your {appt.ServiceType} appointment ({appt.SubService}) on {appt.VisitDate:MMM dd, yyyy} has been cancelled.");

        return true;
    }

    public async Task<List<Appointment>> GetAllForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }

    public async Task<Appointment?> GetNextUpcomingAsync(string patientEmail)
    {
        var all = await GetAllForPatientAsync(patientEmail);
        var now = DateTime.Now;

        return all
            .Where(a => a.Status != "Cancelled" && a.Status != "Completed" && GetVisitDateTime(a) >= now)
            .OrderBy(a => GetVisitDateTime(a))
            .FirstOrDefault();
    }

   

    private static DateTime GetVisitDateTime(Appointment a)
    {
        if (DateTime.TryParse(a.VisitTime, out var parsedTime))
            return a.VisitDate.Date + parsedTime.TimeOfDay;

        // Fallback: no parseable time, treat as end of day so it doesn't vanish early
        return a.VisitDate.Date.AddHours(23).AddMinutes(59);
    }

    public async Task<List<string>> GetBookedTimesAsync(DateTime date, string serviceType, string subService)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.VisitDate.Date == date.Date
                        && a.ServiceType == serviceType
                        && a.SubService == subService
                        && a.Status != "Cancelled")
            .Select(a => a.VisitTime)
            .ToList();
    }

    public async Task<List<Appointment>> GetPendingApprovalsAsync()
    {
        await EnsureInitializedAsync();
        await ExpireOverdueRequestsAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Where(a => a.Status == "Pending").OrderBy(a => a.VisitDate).ToList();
    }

    public async Task<bool> ApproveAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        var appt = await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();
        if (appt == null) return false;

        appt.Status = "Confirmed";
        await _db.UpdateAsync(appt);

        await NotificationService.Instance.AddAsync(
            appt.PatientEmail,
            "Appointment Approved",
            $"Your {appt.ServiceType} appointment on {appt.VisitDate:MMM dd, yyyy} at {appt.VisitTime} has been approved.");

        return true;
    }

    public async Task<Appointment?> GetApprovedAppointmentTodayAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.FirstOrDefault(a =>
            a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            a.VisitDate.Date == DateTime.Today &&
            a.Status == "Confirmed");
    }

    public async Task<Appointment?> GetCheckedInTodayAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.FirstOrDefault(a =>
            a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            a.VisitDate.Date == DateTime.Today &&
            a.Status == "CheckedIn");
    }

    public async Task<Appointment> CheckInAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        var appt = await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();

        var all = await _db.Table<Appointment>().ToListAsync();
        int nextQueueNumber = all.Count(a => a.VisitDate.Date == DateTime.Today && a.QueueNumber.HasValue) + 1;

        appt!.Status = "CheckedIn";
        appt.CheckInTime = DateTime.Now;
        appt.QueueNumber = nextQueueNumber;
        await _db.UpdateAsync(appt);

        return appt;
    }

    public async Task<int> GetTodayTotalCountAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Count(a => a.VisitDate.Date == DateTime.Today);
    }

    public async Task<int> GetTodayCheckedInCountAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Count(a => a.VisitDate.Date == DateTime.Today && a.Status == "CheckedIn");
    }

    public async Task<int> GetPendingApprovalCountAsync()
    {
        await EnsureInitializedAsync();
        await ExpireOverdueRequestsAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Count(a => a.Status == "Pending");
    }

    private async Task ExpireOverdueRequestsAsync()
    {
        var now = DateTime.Now;
        var all = await _db.Table<Appointment>().ToListAsync();

        var overdue = all.Where(a =>
            a.Status == "Pending" &&
            (a.VisitDate.Date < now.Date ||
             (a.VisitDate.Date == now.Date && DateTime.TryParse(a.VisitTime, out var t) && a.VisitDate.Date + t.TimeOfDay < now)));

        foreach (var a in overdue)
        {
            a.Status = "Expired";
            await _db.UpdateAsync(a);
        }
    }

    public async Task<int> GetUnseenCountAsync(string patientEmail)
    {
        var all = await GetAllForPatientAsync(patientEmail);
        return all.Count(a => !a.IsSeenByPatient);
    }


    public async Task<List<Appointment>> GetAllCertRequestsAsync()
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.ServiceType == "Cert Request")
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }



    public async Task<List<Appointment>> GetAllAppointmentsAsync()
    {
        await EnsureInitializedAsync();
        return (await _db.Table<Appointment>().ToListAsync()).OrderByDescending(a => a.VisitDate).ToList();
    }

    public async Task MarkAllSeenAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        var unseen = all.Where(a => a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) && !a.IsSeenByPatient);

        foreach (var a in unseen)
        {
            a.IsSeenByPatient = true;
            await _db.UpdateAsync(a);
        }
    }


    public async Task<List<Appointment>> GetTodayForServiceAsync(string serviceType)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.ServiceType == serviceType
                        && a.VisitDate.Date == DateTime.Today
                        && a.Status != "Cancelled")
            .OrderBy(a => a.VisitTime)
            .ToList();
    }

    public async Task<bool> MarkCompletedAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        var appt = await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();
        if (appt == null) return false;

        appt.Status = "Completed";
        await _db.UpdateAsync(appt);
        return true;
    }

    public async Task<Appointment?> GetByIdAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();
    }

    public async Task<List<Appointment>> GetAllForServiceAsync(string serviceType)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.ServiceType == serviceType)
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }

    public async Task<bool> MarkVitalsRecordedAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        var appointment = await _db.Table<Appointment>()
            .Where(a => a.Id == appointmentId)
            .FirstOrDefaultAsync();

        if (appointment == null)
            return false;

        if (appointment.Status != "CheckedIn" &&
            appointment.Status != "VitalsRecorded")
        {
            return false;
        }

        appointment.Status = "VitalsRecorded";

        await _db.UpdateAsync(appointment);

        return true;
    }

}