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
            await StaffScheduleService.Instance.EnsureInitializedAsync();
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
            ["CertificateCollectedAt"] = "DATETIME NULL",
            ["AssignedStaffId"] = "INTEGER NULL"
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
        appointment.AssignedStaffId = null;

        if (appointment.ServiceType == "Cert Request")
        {
            if (appointment.SubService is not "Medical Certificate" and not "Dental Certificate")
                return new(false, "Choose a medical or dental paper certificate.");

            appointment.CertificateStatus = "Awaiting Appointment Approval";
        }

        string? conflictMessage = null;
        var dayStart = appointment.VisitDate.Date;
        var dayEnd = dayStart.AddDays(1);
        var slotMinutes = await ClinicScheduleService.Instance.GetSlotIntervalMinutesAsync();
        var visitStartsAt = appointment.VisitDate.Date.Add(selectedTime.TimeOfDay);
        var visitEndsAt = visitStartsAt.AddMinutes(slotMinutes);
        var clinicalType = GetClinicalType(appointment);
        var requiredRole = GetClinicalStaffRole(clinicalType);

        await _db.RunInTransactionAsync(connection =>
        {
            var sameDayAppointments = connection.Table<Appointment>()
                .Where(existing => existing.VisitDate >= dayStart && existing.VisitDate < dayEnd)
                .ToList();
            var patientAlreadyBooked = sameDayAppointments.Any(existing =>
                existing.PatientEmail.Equals(appointment.PatientEmail, StringComparison.OrdinalIgnoreCase) &&
                SlotOccupyingStatuses.Contains(existing.Status) &&
                GetVisitDateTime(existing) < visitEndsAt &&
                GetVisitDateTime(existing).AddMinutes(slotMinutes) > visitStartsAt);

            if (patientAlreadyBooked)
            {
                conflictMessage = "You already have an appointment during that time. Please choose another available time.";
                return;
            }

            if (requiredRole != null)
            {
                var availableStaffId = StaffScheduleService.FindAvailableStaffId(
                    connection,
                    requiredRole,
                    visitStartsAt,
                    visitEndsAt,
                    slotMinutes);
                if (!availableStaffId.HasValue)
                {
                    conflictMessage = $"No active {requiredRole.ToLowerInvariant()} is scheduled and available at that time. Choose another time or ask the Clinic Administrator to assign a shift.";
                    return;
                }

                appointment.AssignedStaffId = availableStaffId.Value;
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

            var notificationClinicalType = appointment.ServiceType == "Cert Request"
                ? appointment.SubService.StartsWith("Dental", StringComparison.OrdinalIgnoreCase) ? "Dental" : "Medical"
                : appointment.ServiceType;
            await NotifyAssignedClinicalStaffAsync(
                appointment,
                $"New {notificationClinicalType} Appointment Request",
                $"A new {notificationClinicalType.ToLowerInvariant()} appointment ({appointment.SubService}) was requested for {appointment.VisitDate:MMM dd, yyyy} at {appointment.VisitTime}.");
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

    private static string? GetClinicalType(Appointment appointment)
    {
        if (appointment.ServiceType is "Medical" or "Dental")
            return appointment.ServiceType;

        if (appointment.ServiceType == "Cert Request")
        {
            if (appointment.SubService == "Medical Certificate")
                return "Medical";
            if (appointment.SubService == "Dental Certificate")
                return "Dental";
        }

        return null;
    }

    private static string? GetClinicalStaffRole(string? clinicalType) => clinicalType switch
    {
        "Medical" => "Doctor",
        "Dental" => "Dentist",
        _ => null
    };

    public async Task NotifyAssignedClinicalStaffAsync(
        Appointment appointment,
        string title,
        string message)
    {
        try
        {
            var clinic = GetClinicalType(appointment);
            var staff = new List<UserAccount>();
            if (appointment.AssignedStaffId.HasValue)
            {
                var assigned = await AuthService.Instance.GetAccountByDatabaseIdAsync(appointment.AssignedStaffId.Value);
                if (assigned != null && AuthService.IsActiveClinicStaff(assigned))
                    staff.Add(assigned);
            }
            else
            {
                // Keep older local appointments without an assigned employee visible to the
                // appropriate clinical team until they are reassigned through approval.
                staff = clinic switch
                {
                    "Medical" => await AuthService.Instance.GetAllDoctorsAsync(),
                    "Dental" => await AuthService.Instance.GetAllDentistsAsync(),
                    _ => new List<UserAccount>()
                };
            }

            foreach (var member in staff)
            {
                try
                {
                    await NotificationService.Instance.AddAsync(member.Email, title, message);
                }
                catch
                {
                    // Notification failures must not undo a saved appointment or clinical record.
                }
            }
        }
        catch
        {
            // Notification failures must not undo a saved appointment or clinical record.
        }
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

        var currentStaff = AuthService.Instance.CurrentUser;
        var all = await GetAllCertRequestsAsync();
        return all
            .Where(appointment =>
                appointment.SubService == requiredType &&
                appointment.Status == "Completed" &&
                appointment.CertificateStatus == "Awaiting Clinical Review" &&
                (!appointment.AssignedStaffId.HasValue || appointment.AssignedStaffId == currentStaff?.Id))
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
                appointment.CertificateStatus != "Awaiting Clinical Review" ||
                (appointment.AssignedStaffId.HasValue && appointment.AssignedStaffId != reviewer.Id))
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

        var cancelledClinic = GetClinicalType(cancelledAppointment);
        if (cancelledClinic != null)
        {
            await NotifyAssignedClinicalStaffAsync(
                cancelledAppointment,
                $"{cancelledClinic} Appointment Cancelled",
                $"{cancelledAppointment.PatientEmail}'s {cancelledClinic.ToLowerInvariant()} appointment ({cancelledAppointment.SubService}) on {cancelledAppointment.VisitDate:MMM dd, yyyy} was cancelled.");
        }

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

        var clinicalType = GetClinicalType(new Appointment
        {
            ServiceType = serviceType,
            SubService = subService
        });
        var requiredRole = GetClinicalStaffRole(clinicalType);
        if (requiredRole == null)
            return new List<string>();

        var availableOptions = await ClinicScheduleService.Instance
            .GetAvailableTimeOptionsAsync(date, serviceType, subService);
        var slotMinutes = await ClinicScheduleService.Instance.GetSlotIntervalMinutesAsync();
        var unavailableTimes = new List<string>();

        await _db.RunInTransactionAsync(connection =>
        {
            foreach (var option in availableOptions)
            {
                if (!DateTime.TryParse(option, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
                {
                    unavailableTimes.Add(option);
                    continue;
                }

                var startsAt = date.Date.Add(parsed.TimeOfDay);
                var endsAt = startsAt.AddMinutes(slotMinutes);
                if (!StaffScheduleService.FindAvailableStaffId(
                        connection, requiredRole, startsAt, endsAt, slotMinutes).HasValue)
                    unavailableTimes.Add(option);
            }
        });

        return unavailableTimes;
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
        var slotMinutes = await ClinicScheduleService.Instance.GetSlotIntervalMinutesAsync();
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

            var clinicalType = GetClinicalType(appointment);
            var requiredRole = GetClinicalStaffRole(clinicalType);
            if (requiredRole != null)
            {
                var startsAt = GetVisitDateTime(appointment);
                var availableStaffId = StaffScheduleService.FindAvailableStaffId(
                    connection,
                    requiredRole,
                    startsAt,
                    startsAt.AddMinutes(slotMinutes),
                    slotMinutes,
                    appointment.Id);
                if (!availableStaffId.HasValue)
                    return;
                appointment.AssignedStaffId = availableStaffId.Value;
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

        var approvalClinic = GetClinicalType(approvedAppointment);
        if (approvalClinic != null)
        {
            await NotifyAssignedClinicalStaffAsync(
                approvedAppointment,
                $"{approvalClinic} Appointment Confirmed",
                $"{approvedAppointment.PatientEmail}'s {approvalClinic.ToLowerInvariant()} appointment ({approvedAppointment.SubService}) was confirmed for {approvedAppointment.VisitDate:MMM dd, yyyy} at {approvedAppointment.VisitTime}.");
        }

        return true;
    }

    public async Task<bool> DeclinePendingAsync(int appointmentId, string reason)
    {
        if (AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar" ||
            string.IsNullOrWhiteSpace(reason))
        {
            return false;
        }

        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        Appointment? declinedAppointment = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.Status != "Pending" ||
                GetVisitDateTime(appointment) <= DateTime.Now)
            {
                return;
            }

            appointment.Status = "Declined";
            if (appointment.ServiceType == "Cert Request")
                appointment.CertificateStatus = "Declined";

            connection.Update(appointment);
            declinedAppointment = appointment;
        });

        if (declinedAppointment == null)
            return false;

        await NotificationService.Instance.AddAsync(
            declinedAppointment.PatientEmail,
            "Appointment Request Declined",
            $"Your {declinedAppointment.ServiceType} request for {declinedAppointment.VisitDate:MMM dd, yyyy} at {declinedAppointment.VisitTime} was declined. Reason: {reason.Trim()}");

        return true;
    }
    public async Task<Appointment?> GetApprovedAppointmentTodayAsync(string patientEmail)
    {
        if (AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
            return null;

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
            a.CheckInTime.HasValue &&
            (a.Status is "CheckedIn" or "VitalsRecorded"));
    }

    public async Task<Appointment?> CheckInAsync(int appointmentId)
    {
        if (AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
            return null;

        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        var appointmentForSchedule = await _db.FindAsync<Appointment>(appointmentId);
        if (appointmentForSchedule == null ||
            appointmentForSchedule.VisitDate.Date != DateTime.Today)
        {
            return null;
        }

        var clinicClosing = await ClinicScheduleService.Instance
            .GetClinicClosingDateTimeAsync(
                appointmentForSchedule.VisitDate,
                appointmentForSchedule.ServiceType,
                appointmentForSchedule.SubService);

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

            var now = DateTime.Now;
            var checkInCutoff = clinicClosing ?? GetVisitDateTime(appointment);
            if (now >= checkInCutoff)
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

            appointment.CheckInTime = now;
            appointment.QueueNumber = nextQueueNumber;
            connection.Update(appointment);
            checkedInAppointment = appointment;
        });

        if (checkedInAppointment != null)
        {
            var clinic = GetClinicalType(checkedInAppointment);
            if (clinic != null)
            {
                await NotifyAssignedClinicalStaffAsync(
                    checkedInAppointment,
                    $"{clinic} Patient Checked In",
                    $"{checkedInAppointment.PatientEmail} checked in for a {clinic.ToLowerInvariant()} appointment at {checkedInAppointment.VisitTime}.");
            }
        }

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

        foreach (var appointment in active)
        {
            var shouldExpirePending =
                appointment.Status == "Pending" &&
                GetVisitDateTime(appointment) <= now;

            var clinicClosing = appointment.VisitDate.Date == now.Date &&
                                appointment.Status == "Confirmed"
                ? await ClinicScheduleService.Instance.GetClinicClosingDateTimeAsync(
                    appointment.VisitDate,
                    appointment.ServiceType,
                    appointment.SubService)
                : null;

            var shouldMarkConfirmedMissed =
                appointment.Status == "Confirmed" &&
                (appointment.VisitDate.Date < now.Date ||
                 (appointment.VisitDate.Date == now.Date &&
                  now >= (clinicClosing ?? GetVisitDateTime(appointment))));

            if (!shouldExpirePending && !shouldMarkConfirmedMissed)
                continue;

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
        await ExpireElapsedAppointmentsAsync();
        return (await _db.Table<Appointment>().ToListAsync())
            .OrderByDescending(a => a.VisitDate)
            .ToList();
    }

    public async Task ReassignAppointmentsFromStaffAsync(int staffId)
    {
        await EnsureInitializedAsync();
        var slotMinutes = await ClinicScheduleService.Instance.GetSlotIntervalMinutesAsync();
        await _db.RunInTransactionAsync(connection =>
        {
            var assigned = connection.Table<Appointment>()
                .ToList()
                .Where(appointment => (!appointment.AssignedStaffId.HasValue || appointment.AssignedStaffId == staffId) &&
                                      appointment.VisitDate.Date >= DateTime.Today &&
                                      (appointment.Status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded"))
                .OrderBy(GetVisitDateTime)
                .ToList();

            foreach (var appointment in assigned)
            {
                var role = GetClinicalStaffRole(GetClinicalType(appointment));
                if (role == null) continue;

                var startsAt = GetVisitDateTime(appointment);
                appointment.AssignedStaffId = StaffScheduleService.FindAvailableStaffId(
                    connection,
                    role,
                    startsAt,
                    startsAt.AddMinutes(slotMinutes),
                    slotMinutes,
                    appointment.Id);
                connection.Update(appointment);
            }
        });
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

    public async Task<bool> MarkCompletedAsync(int appointmentId, int? actingStaffId = null)
    {
        await EnsureInitializedAsync();

        var completed = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.Status is not ("CheckedIn" or "VitalsRecorded") ||
                (actingStaffId.HasValue && appointment.AssignedStaffId.HasValue &&
                 appointment.AssignedStaffId != actingStaffId))
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
        if (AuthService.Instance.CurrentUser?.Role != "Nurse")
            return false;

        var savedVitals = await VitalSignsService.Instance.GetForAppointmentAsync(appointmentId);
        if (savedVitals == null)
            return false;

        await EnsureInitializedAsync();

        var recorded = false;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.VisitDate.Date != DateTime.Today ||
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
