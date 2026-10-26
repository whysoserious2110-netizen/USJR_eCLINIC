using System.Globalization;
using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

public sealed class AppointmentColumnInfo
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed record AppointmentBookingResult(bool Success, string Message);

public class AppointmentService
{
    public static AppointmentService Instance { get; } = new();

    private static readonly HashSet<string> SlotOccupyingStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Pending",
            "Confirmed",
            "CheckedIn",
            "VitalsRecorded"
        };

    private readonly SQLiteAsyncConnection _db = ClinicDatabase.Connection;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private AppointmentService()
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

            await _db.CreateTableAsync<Appointment>();
            await EnsureCertificateWorkflowColumnsAsync();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task EnsureCertificateWorkflowColumnsAsync()
    {
        var columns = await _db.QueryAsync<AppointmentColumnInfo>("PRAGMA table_info('Appointment');");
        var existing = new HashSet<string>(
            columns.Select(column => column.Name),
            StringComparer.OrdinalIgnoreCase);

        var requiredColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CertificateStatus"] = "TEXT NOT NULL DEFAULT ''",
            ["CertificateDecisionNote"] = "TEXT NOT NULL DEFAULT ''",
            ["CertificateReviewedBy"] = "TEXT NOT NULL DEFAULT ''",
            ["CertificateReviewedAt"] = "DATETIME NULL",
            ["CertificateCollectedBy"] = "TEXT NOT NULL DEFAULT ''",
            ["CertificateCollectedAt"] = "DATETIME NULL"
        };

        foreach (var column in requiredColumns)
        {
            if (existing.Contains(column.Key))
                continue;

            await _db.ExecuteAsync(
                $"ALTER TABLE Appointment ADD COLUMN {column.Key} {column.Value}");
        }

        var legacyRequests = await _db.Table<Appointment>()
            .Where(appointment =>
                appointment.ServiceType == "Cert Request" &&
                appointment.CertificateStatus == string.Empty)
            .ToListAsync();

        foreach (var appointment in legacyRequests)
        {
            appointment.CertificateStatus = appointment.Status switch
            {
                "Pending" => "Awaiting Appointment Approval",
                "Confirmed" => "Awaiting Clinic Visit",
                "CheckedIn" or "VitalsRecorded" => "Visit In Progress",
                "Completed" => "Awaiting Clinical Review",
                "Cancelled" => "Cancelled",
                "Expired" => "Expired",
                "Missed" => "Missed Appointment",
                _ => "Awaiting Appointment Approval"
            };

            await _db.UpdateAsync(appointment);
        }
    }
    public async Task<AppointmentBookingResult> BookAsync(Appointment appointment)
    {
        await EnsureInitializedAsync();

        if (string.IsNullOrWhiteSpace(appointment.PatientEmail) ||
            string.IsNullOrWhiteSpace(appointment.ServiceType) ||
            string.IsNullOrWhiteSpace(appointment.SubService))
        {
            return new(false, "Some appointment details are missing. Please review the form.");
        }

        var scheduleError = await ClinicScheduleService.Instance.ValidateSlotAsync(
            appointment.VisitDate,
            appointment.ServiceType,
            appointment.SubService,
            appointment.VisitTime);

        if (scheduleError != null)
            return new(false, scheduleError);

        if (!DateTime.TryParse(
                appointment.VisitTime,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var selectedTime))
        {
            return new(false, "Please choose a valid appointment time.");
        }

        appointment.VisitTime = selectedTime.ToString("h:mm tt", CultureInfo.CurrentCulture);
        appointment.Status = "Pending";
        appointment.VisitDate = appointment.VisitDate.Date;

        if (appointment.ServiceType == "Cert Request")
        {
            if (appointment.SubService is not "Medical Certificate" and not "Dental Certificate")
                return new(false, "Choose a medical or dental paper certificate.");

            appointment.CertificateStatus = "Awaiting Appointment Approval";
        }

        string? conflictMessage = null;
        var dayStart = appointment.VisitDate.Date;
        var dayEnd = dayStart.AddDays(1);

        await _db.RunInTransactionAsync(connection =>
        {
            var sameServiceSlots = connection.Table<Appointment>()
                .Where(a =>
                    a.VisitDate >= dayStart &&
                    a.VisitDate < dayEnd &&
                    a.ServiceType == appointment.ServiceType &&
                    a.SubService == appointment.SubService)
                .ToList();

            bool slotIsTaken = sameServiceSlots.Any(a =>
                SlotOccupyingStatuses.Contains(a.Status) &&
                string.Equals(
                    NormalizeTime(a.VisitTime),
                    NormalizeTime(appointment.VisitTime),
                    StringComparison.Ordinal));

            if (slotIsTaken)
            {
                conflictMessage = "That appointment time has just been booked. Please choose another available time.";
                return;
            }

            connection.Insert(appointment);
        });

        if (conflictMessage != null)
            return new(false, conflictMessage);

        var notificationsSaved = true;
        try
        {
            await NotificationService.Instance.AddAsync(
                appointment.PatientEmail,
                "Appointment Requested",
                $"Your {appointment.ServiceType} appointment ({appointment.SubService}) on {appointment.VisitDate:MMM dd, yyyy} at {appointment.VisitTime} has been submitted and is Pending.");

            if (appointment.ServiceType == "Medical")
            {
                var doctors = await AuthService.Instance.GetAllDoctorsAsync();
                foreach (var doctor in doctors)
                {
                    await NotificationService.Instance.AddAsync(
                        doctor.Email,
                        "New Appointment Request",
                        $"A new Medical appointment ({appointment.SubService}) was booked for {appointment.VisitDate:MMM dd, yyyy} at {appointment.VisitTime}.");
                }
            }
        }
        catch
        {
            // The appointment is already saved. A local notification failure must not
            // make the screen tell the patient that the request itself failed.
            notificationsSaved = false;
        }

        return notificationsSaved
            ? new(true, "Your appointment request has been submitted. Status: Pending.")
            : new(true, "Your appointment request was saved, but a notification could not be created.");
    }

    private static string NormalizeTime(string value)
    {
        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("HH:mm", CultureInfo.InvariantCulture)
            : value.Trim();
    }

    public async Task<List<Appointment>> GetCertificateRequestsForClinicalReviewAsync(string staffRole)
    {
        var requiredType = staffRole switch
        {
            "Doctor" => "Medical Certificate",
            "Dentist" => "Dental Certificate",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(requiredType))
            return new List<Appointment>();

        var all = await GetAllCertRequestsAsync();
        return all
            .Where(appointment =>
                appointment.SubService == requiredType &&
                appointment.Status == "Completed" &&
                appointment.CertificateStatus == "Awaiting Clinical Review")
            .OrderBy(GetVisitDateTime)
            .ToList();
    }

    public async Task<bool> ReviewCertificateRequestAsync(
        int appointmentId,
        bool approve,
        string decisionNote)
    {
        await EnsureInitializedAsync();

        var reviewer = AuthService.Instance.CurrentUser;
        if (reviewer?.Role is not ("Doctor" or "Dentist"))
            return false;

        var allowedType = reviewer.Role == "Doctor"
            ? "Medical Certificate"
            : "Dental Certificate";

        if (!approve && string.IsNullOrWhiteSpace(decisionNote))
            return false;

        Appointment? reviewedAppointment = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.ServiceType != "Cert Request" ||
                appointment.SubService != allowedType ||
                appointment.Status != "Completed" ||
                appointment.CertificateStatus != "Awaiting Clinical Review")
            {
                return;
            }

            appointment.CertificateStatus = approve ? "Ready for Pickup" : "Declined";
            appointment.CertificateDecisionNote = decisionNote.Trim();
            appointment.CertificateReviewedBy = reviewer.FullName;
            appointment.CertificateReviewedAt = DateTime.Now;
            connection.Update(appointment);
            reviewedAppointment = appointment;
        });

        if (reviewedAppointment == null)
            return false;

        var notificationMessage = approve
            ? $"Your paper {reviewedAppointment.SubService.ToLowerInvariant()} is ready for pickup at the clinic."
            : $"Your paper {reviewedAppointment.SubService.ToLowerInvariant()} request was declined. Reason: {reviewedAppointment.CertificateDecisionNote}";

        try
        {
            await NotificationService.Instance.AddAsync(
                reviewedAppointment.PatientEmail,
                approve ? "Certificate Ready for Pickup" : "Certificate Request Update",
                notificationMessage);
        }
        catch
        {
            // Keep the clinician's saved decision even if a local notification cannot be created.
        }

        return true;
    }

    public async Task<bool> MarkCertificateCollectedAsync(int appointmentId)
    {
        await EnsureInitializedAsync();

        var readsStaff = AuthService.Instance.CurrentUser;
        if (readsStaff?.Role != "R.E.A.D.S. Scholar")
            return false;

        Appointment? collectedAppointment = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.ServiceType != "Cert Request" ||
                appointment.CertificateStatus != "Ready for Pickup")
            {
                return;
            }

            appointment.CertificateStatus = "Collected";
            appointment.CertificateCollectedBy = readsStaff.FullName;
            appointment.CertificateCollectedAt = DateTime.Now;
            connection.Update(appointment);
            collectedAppointment = appointment;
        });

        if (collectedAppointment == null)
            return false;

        try
        {
            await NotificationService.Instance.AddAsync(
                collectedAppointment.PatientEmail,
                "Certificate Collected",
                $"Your paper {collectedAppointment.SubService.ToLowerInvariant()} was marked as collected.");
        }
        catch
        {
            // Keep the pickup record even if a local notification cannot be created.
        }

        return true;
    }
    public async Task<bool> CancelAsync(int appointmentId)
    {
        await EnsureInitializedAsync();

        Appointment? cancelledAppointment = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                (appointment.Status != "Pending" && appointment.Status != "Confirmed"))
            {
                return;
            }

            appointment.Status = "Cancelled";
            if (appointment.ServiceType == "Cert Request")
                appointment.CertificateStatus = "Cancelled";

            connection.Update(appointment);
            cancelledAppointment = appointment;
        });

        if (cancelledAppointment == null)
            return false;

        await NotificationService.Instance.AddAsync(
            cancelledAppointment.PatientEmail,
            "Appointment Cancelled",
            $"Your {cancelledAppointment.ServiceType} appointment ({cancelledAppointment.SubService}) on {cancelledAppointment.VisitDate:MMM dd, yyyy} has been cancelled.");

        return true;
    }

    public async Task<List<Appointment>> GetAllForPatientAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        var all = await _db.Table<Appointment>().ToListAsync();

        return all
            .Where(a => a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(GetVisitDateTime)
            .ToList();
    }

    public async Task<List<Appointment>> GetUpcomingForPatientAsync(string patientEmail)
    {
        var all = await GetAllForPatientAsync(patientEmail);
        var now = DateTime.Now;

        return all
            .Where(a => SlotOccupyingStatuses.Contains(a.Status) &&
                        GetVisitDateTime(a) > now)
            .OrderBy(GetVisitDateTime)
            .ToList();
    }

    public async Task<Appointment?> GetNextUpcomingAsync(string patientEmail)
    {
        var all = await GetAllForPatientAsync(patientEmail);
        var now = DateTime.Now;

        return all
            .Where(a =>
                SlotOccupyingStatuses.Contains(a.Status) &&
                GetVisitDateTime(a) >= now)
            .OrderBy(GetVisitDateTime)
            .FirstOrDefault();
    }

    public static DateTime GetVisitDateTime(Appointment appointment)
    {
        if (DateTime.TryParse(appointment.VisitTime, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsedTime))
            return appointment.VisitDate.Date + parsedTime.TimeOfDay;

        return appointment.VisitDate.Date.AddHours(23).AddMinutes(59);
    }

    public async Task<List<string>> GetBookedTimesAsync(
        DateTime date,
        string serviceType,
        string subService)
    {
        await EnsureInitializedAsync();

        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);
        var matches = await _db.Table<Appointment>()
            .Where(a =>
                a.VisitDate >= dayStart &&
                a.VisitDate < dayEnd &&
                a.ServiceType == serviceType &&
                a.SubService == subService)
            .ToListAsync();

        return matches
            .Where(a => SlotOccupyingStatuses.Contains(a.Status))
            .Select(a => a.VisitTime)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<Appointment>> GetPendingApprovalsAsync()
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Where(a => a.Status == "Pending")
            .OrderBy(a => a.VisitDate)
            .ToList();
    }

    public async Task<bool> ApproveAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        Appointment? approvedAppointment = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null || appointment.Status != "Pending")
                return;

            if (GetVisitDateTime(appointment) <= DateTime.Now)
            {
                appointment.Status = "Expired";
                if (appointment.ServiceType == "Cert Request")
                    appointment.CertificateStatus = "Expired";

                connection.Update(appointment);
                return;
            }

            appointment.Status = "Confirmed";
            if (appointment.ServiceType == "Cert Request")
                appointment.CertificateStatus = "Awaiting Clinic Visit";

            connection.Update(appointment);
            approvedAppointment = appointment;
        });

        if (approvedAppointment == null)
            return false;

        await NotificationService.Instance.AddAsync(
            approvedAppointment.PatientEmail,
            "Appointment Approved",
            $"Your {approvedAppointment.ServiceType} appointment on {approvedAppointment.VisitDate:MMM dd, yyyy} at {approvedAppointment.VisitTime} has been approved.");

        return true;
    }

    public async Task<Appointment?> GetApprovedAppointmentTodayAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var all = await _db.Table<Appointment>()
            .Where(a => a.VisitDate >= today && a.VisitDate < tomorrow)
            .ToListAsync();

        return all.FirstOrDefault(a =>
            a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            a.Status == "Confirmed");
    }

    public async Task<Appointment?> GetCheckedInTodayAsync(string patientEmail)
    {
        await EnsureInitializedAsync();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var all = await _db.Table<Appointment>()
            .Where(a => a.VisitDate >= today && a.VisitDate < tomorrow)
            .ToListAsync();

        return all.FirstOrDefault(a =>
            a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            a.CheckInTime.HasValue);
    }

    public async Task<Appointment?> CheckInAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        Appointment? checkedInAppointment = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.Status != "Confirmed" ||
                appointment.VisitDate.Date != DateTime.Today)
            {
                return;
            }

            if (GetVisitDateTime(appointment) <= DateTime.Now)
            {
                appointment.Status = "Missed";
                if (appointment.ServiceType == "Cert Request")
                    appointment.CertificateStatus = "Missed Appointment";

                connection.Update(appointment);
                return;
            }

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var existingQueueNumbers = connection.Table<Appointment>()
                .Where(a => a.VisitDate >= today && a.VisitDate < tomorrow)
                .ToList()
                .Where(a => a.QueueNumber.HasValue)
                .Select(a => a.QueueNumber!.Value);

            var nextQueueNumber = existingQueueNumbers.DefaultIfEmpty(0).Max() + 1;

            appointment.Status = "CheckedIn";
            if (appointment.ServiceType == "Cert Request")
                appointment.CertificateStatus = "Visit In Progress";

            appointment.CheckInTime = DateTime.Now;
            appointment.QueueNumber = nextQueueNumber;
            connection.Update(appointment);
            checkedInAppointment = appointment;
        });

        return checkedInAppointment;
    }

    public async Task<int> GetTodayTotalCountAsync()
    {
        await EnsureInitializedAsync();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        return await _db.Table<Appointment>()
            .Where(a => a.VisitDate >= today && a.VisitDate < tomorrow)
            .CountAsync();
    }

    public async Task<int> GetTodayCheckedInCountAsync()
    {
        await EnsureInitializedAsync();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var all = await _db.Table<Appointment>()
            .Where(a => a.VisitDate >= today && a.VisitDate < tomorrow)
            .ToListAsync();

        return all.Count(a => a.Status == "CheckedIn" || a.Status == "VitalsRecorded");
    }

    public async Task<int> GetPendingApprovalCountAsync()
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();
        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Count(a => a.Status == "Pending");
    }

    private async Task ExpireElapsedAppointmentsAsync()
    {
        var now = DateTime.Now;
        var active = await _db.Table<Appointment>()
            .Where(a => a.Status == "Pending" || a.Status == "Confirmed")
            .ToListAsync();

        var elapsed = active.Where(a => GetVisitDateTime(a) <= now);

        foreach (var appointment in elapsed)
        {
            var wasPending = appointment.Status == "Pending";
            appointment.Status = wasPending ? "Expired" : "Missed";

            if (appointment.ServiceType == "Cert Request")
                appointment.CertificateStatus = wasPending ? "Expired" : "Missed Appointment";

            await _db.UpdateAsync(appointment);
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
        await ExpireElapsedAppointmentsAsync();
        var all = await _db.Table<Appointment>().ToListAsync();

        return all
            .Where(a => a.ServiceType == "Cert Request")
            .OrderByDescending(GetVisitDateTime)
            .ToList();
    }

    public async Task<int> GetReadyCertificatePickupCountAsync()
    {
        var requests = await GetAllCertRequestsAsync();
        return requests.Count(appointment => appointment.CertificateStatus == "Ready for Pickup");
    }

    public async Task<List<Appointment>> GetAllAppointmentsAsync()
    {
        await EnsureInitializedAsync();
        return (await _db.Table<Appointment>().ToListAsync())
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }

    public async Task MarkAllSeenAsync(string patientEmail)
    {
        await EnsureInitializedAsync();

        var all = await _db.Table<Appointment>().ToListAsync();
        var unseen = all.Where(a =>
            a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
            !a.IsSeenByPatient);

        foreach (var appointment in unseen)
        {
            appointment.IsSeenByPatient = true;
            await _db.UpdateAsync(appointment);
        }
    }

    public async Task<List<Appointment>> GetTodayForServiceAsync(string serviceType)
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var all = await _db.Table<Appointment>()
            .Where(a =>
                a.ServiceType == serviceType &&
                a.VisitDate >= today &&
                a.VisitDate < tomorrow)
            .ToListAsync();

        return all
            .Where(a => a.Status != "Cancelled" && a.Status != "Expired" &&
                        a.Status != "Missed")
            .OrderBy(a => GetVisitDateTime(a))
            .ToList();
    }

    public async Task<bool> MarkCompletedAsync(int appointmentId)
    {
        await EnsureInitializedAsync();

        var completed = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.Status is not ("CheckedIn" or "VitalsRecorded"))
            {
                return;
            }

            appointment.Status = "Completed";
            if (appointment.ServiceType == "Cert Request" &&
                appointment.CertificateStatus is "Visit In Progress" or "Awaiting Clinic Visit")
            {
                appointment.CertificateStatus = "Awaiting Clinical Review";
            }

            connection.Update(appointment);
            completed = true;
        });

        return completed;
    }
    public async Task<Appointment?> GetByIdAsync(int appointmentId)
    {
        await EnsureInitializedAsync();
        return await _db.Table<Appointment>()
            .Where(a => a.Id == appointmentId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Appointment>> GetAllForServiceAsync(string serviceType)
    {
        await EnsureInitializedAsync();
        var all = await _db.Table<Appointment>()
            .Where(a => a.ServiceType == serviceType)
            .ToListAsync();

        return all.OrderByDescending(a => a.VisitDate).ToList();
    }

    public async Task<bool> MarkVitalsRecordedAsync(int appointmentId)
    {
        await EnsureInitializedAsync();

        var recorded = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                (appointment.Status != "CheckedIn" && appointment.Status != "VitalsRecorded"))
            {
                return;
            }

            appointment.Status = "VitalsRecorded";
            connection.Update(appointment);
            recorded = true;
        });

        return recorded;
    }
}
