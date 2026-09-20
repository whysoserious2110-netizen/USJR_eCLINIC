using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public class AppointmentService
{
    public static AppointmentService Instance { get; } = new AppointmentService();

    private readonly SQLiteAsyncConnection _db;

    private AppointmentService()
    {
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "usjr_eclinic.db3");
        _db = new SQLiteAsyncConnection(dbPath);
        _db.CreateTableAsync<Appointment>().Wait();
    }

    public async Task BookAsync(Appointment appointment)
    {
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
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.ServiceType == "Cert Request" && (a.Status == "Pending" || a.Status == "Confirmed"))
            .OrderBy(a => a.VisitDate)
            .ToList();
    }

    public async Task<bool> IssueCertificateAsync(int appointmentId, string content)
    {
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
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }

    public async Task<Appointment?> GetNextUpcomingAsync(string patientEmail)
    {
        var all = await GetAllForPatientAsync(patientEmail);
        return all
            .Where(a => a.VisitDate.Date >= DateTime.Today && a.Status != "Cancelled")
            .OrderBy(a => a.VisitDate)
            .FirstOrDefault();
    }

    public async Task<List<string>> GetBookedTimesAsync(DateTime date, string serviceType, string subService)
    {
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
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Where(a => a.Status == "Pending").OrderBy(a => a.VisitDate).ToList();
    }

    public async Task<bool> ApproveAsync(int appointmentId)
    {
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
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.FirstOrDefault(a =>
            a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            a.VisitDate.Date == DateTime.Today &&
            a.Status == "Confirmed");
    }

    public async Task<Appointment?> GetCheckedInTodayAsync(string patientEmail)
    {
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.FirstOrDefault(a =>
            a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            a.VisitDate.Date == DateTime.Today &&
            a.Status == "CheckedIn");
    }

    public async Task<Appointment> CheckInAsync(int appointmentId)
    {
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
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Count(a => a.VisitDate.Date == DateTime.Today);
    }

    public async Task<int> GetTodayCheckedInCountAsync()
    {
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Count(a => a.VisitDate.Date == DateTime.Today && a.Status == "CheckedIn");
    }

    public async Task<int> GetPendingApprovalCountAsync()
    {
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Count(a => a.Status == "Pending");
    }

    public async Task<int> GetUnseenCountAsync(string patientEmail)
    {
        var all = await GetAllForPatientAsync(patientEmail);
        return all.Count(a => !a.IsSeenByPatient);
    }


    public async Task<List<Appointment>> GetAllCertRequestsAsync()
    {
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.ServiceType == "Cert Request")
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }



    public async Task<List<Appointment>> GetAllAppointmentsAsync()
    {
        return (await _db.Table<Appointment>().ToListAsync()).OrderByDescending(a => a.VisitDate).ToList();
    }

    public async Task MarkAllSeenAsync(string patientEmail)
    {
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
        var appt = await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();
        if (appt == null) return false;

        appt.Status = "Completed";
        await _db.UpdateAsync(appt);
        return true;
    }

    public async Task<Appointment?> GetByIdAsync(int appointmentId)
        => await _db.Table<Appointment>().Where(a => a.Id == appointmentId).FirstOrDefaultAsync();





public async Task<List<Appointment>> GetAllForServiceAsync(string serviceType)
    {
        var all = await _db.Table<Appointment>().ToListAsync();
        return all
            .Where(a => a.ServiceType == serviceType)
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }



    public async Task<bool> MarkVitalsRecordedAsync(int appointmentId)
    {
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