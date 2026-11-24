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
public sealed record AppointmentCheckInResult(
    bool Success,
    Appointment? Appointment,
    string Message,
    bool IsLate = false,
    bool RequiresReschedule = false);

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

    private static readonly HashSet<string> ActiveFollowUpRequestStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Pending", "Confirmed", "Reschedule Requested", "Reschedule Required", "CheckedIn", "VitalsRecorded"
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
            ["AssignedStaffId"] = "INTEGER NULL",
            ["OriginalAppointmentId"] = "INTEGER NULL",
            ["RecommendedByStaffId"] = "INTEGER NULL",
            ["RecommendedFollowUpDate"] = "DATETIME NULL",
            ["FollowUpInstructions"] = "TEXT NOT NULL DEFAULT ''",
            ["ReviewingStaffId"] = "INTEGER NULL",
            ["ReviewRemarks"] = "TEXT NOT NULL DEFAULT ''",
            ["ProposedVisitDate"] = "DATETIME NULL",
            ["ProposedVisitTime"] = "TEXT NOT NULL DEFAULT ''",
            ["SuggestedScheduleStatus"] = "TEXT NOT NULL DEFAULT ''",
            ["AssignedShift"] = "TEXT NOT NULL DEFAULT ''",
            ["RescheduledFromAppointmentId"] = "INTEGER NULL",
            ["RescheduledToAppointmentId"] = "INTEGER NULL",
            ["ReschedulingReason"] = "TEXT NOT NULL DEFAULT ''",
            ["RescheduledAt"] = "DATETIME NULL"
        };

        foreach (var column in requiredColumns)
        {
            if (existing.Contains(column.Key))
                continue;

            await _db.ExecuteAsync(
                $"ALTER TABLE Appointment ADD COLUMN {column.Key} {column.Value}");
        }

        await _db.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_Appointment_OriginalAppointmentId ON Appointment (OriginalAppointmentId)");
        await _db.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_Appointment_RescheduledFromAppointmentId ON Appointment (RescheduledFromAppointmentId)");

        var appointmentsWithoutShift = (await _db.Table<Appointment>().ToListAsync())
            .Where(appointment => string.IsNullOrWhiteSpace(appointment.AssignedShift))
            .ToList();
        foreach (var appointment in appointmentsWithoutShift)
        {
            if (!AppointmentShiftPolicy.TryParseTime(appointment.VisitTime, out var visitTime))
                continue;

            var shift = AppointmentShiftPolicy.GetShiftForAppointmentStart(visitTime);
            if (shift == null)
                continue;

            appointment.AssignedShift = shift;
            await _db.UpdateAsync(appointment);
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

        if (IsFollowUpAppointment(appointment))
        {
            var currentPatient = AuthService.Instance.CurrentUser;
            if (currentPatient == null ||
                !IsPatientRole(currentPatient.Role) ||
                !currentPatient.Email.Equals(appointment.PatientEmail, StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "Sign in with the patient account that owns the selected previous visit.");
            }

            if (!appointment.OriginalAppointmentId.HasValue)
                return new(false, "Choose the completed visit that your follow-up is for.");

            var originalAppointment = await GetByIdAsync(appointment.OriginalAppointmentId.Value);
            if (originalAppointment == null ||
                originalAppointment.Status != "Completed" ||
                !originalAppointment.PatientEmail.Equals(appointment.PatientEmail, StringComparison.OrdinalIgnoreCase) ||
                GetClinicalType(originalAppointment) != appointment.ServiceType)
            {
                return new(false, "The selected previous visit is not available for this follow-up.");
            }

            if (appointment.ServiceType == "Medical")
            {
                var sourceRecord = await MedicalRecordService.Instance.GetByAppointmentIdAsync(originalAppointment.Id);
                if (sourceRecord == null || !sourceRecord.FollowUpRecommended ||
                    !sourceRecord.PatientEmail.Equals(appointment.PatientEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return new(false, "A Doctor must recommend a medical follow-up for the selected visit first.");
                }

                appointment.RecommendedByStaffId = sourceRecord.AttendingStaffId > 0 ? sourceRecord.AttendingStaffId : null;
                appointment.RecommendedFollowUpDate = sourceRecord.RecommendedFollowUpDate;
                appointment.FollowUpInstructions = sourceRecord.FollowUpNotes;
            }
            else
            {
                var sourceRecord = await DentalRecordService.Instance.GetByAppointmentIdAsync(originalAppointment.Id);
                if (sourceRecord == null || !sourceRecord.FollowUpRecommended ||
                    !sourceRecord.PatientEmail.Equals(appointment.PatientEmail, StringComparison.OrdinalIgnoreCase))
                {
                    return new(false, "A Dentist must recommend a dental follow-up for the selected visit first.");
                }

                appointment.RecommendedByStaffId = sourceRecord.AttendingDentistId > 0 ? sourceRecord.AttendingDentistId : null;
                appointment.RecommendedFollowUpDate = sourceRecord.RecommendedFollowUpDate;
                appointment.FollowUpInstructions = sourceRecord.CareInstructions;
            }
        }
        else if (appointment.OriginalAppointmentId.HasValue)
        {
            return new(false, "Only a follow-up appointment can be linked to a previous visit.");
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
        appointment.AssignedShift = AppointmentShiftPolicy.GetShiftForAppointmentStart(selectedTime.TimeOfDay) ?? string.Empty;
        if (string.IsNullOrEmpty(appointment.AssignedShift))
            return new(false, AppointmentShiftPolicy.GetSlotPolicyError(selectedTime.TimeOfDay));
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
        const int slotMinutes = AppointmentShiftPolicy.AppointmentDurationMinutes;
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

            if (IsFollowUpAppointment(appointment) &&
                connection.Table<Appointment>().ToList().Any(existing =>
                    existing.OriginalAppointmentId == appointment.OriginalAppointmentId &&
                    ActiveFollowUpRequestStatuses.Contains(existing.Status)))
            {
                conflictMessage = "An active follow-up request already exists for that previous visit. Open My Appointments to review it.";
                return;
            }

            if (requiredRole != null)
            {
                int? availableStaffId = null;
                if (IsFollowUpAppointment(appointment) &&
                    appointment.RecommendedByStaffId.HasValue &&
                    StaffScheduleService.IsStaffAvailable(
                        connection,
                        appointment.RecommendedByStaffId.Value,
                        requiredRole,
                        visitStartsAt,
                        visitEndsAt,
                        slotMinutes))
                {
                    availableStaffId = appointment.RecommendedByStaffId.Value;
                }
                else
                {
                    availableStaffId = StaffScheduleService.FindAvailableStaffId(
                        connection,
                        requiredRole,
                        visitStartsAt,
                        visitEndsAt,
                        slotMinutes);
                }
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

    public static string? GetClinicalType(Appointment appointment)
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

    public static bool IsFollowUpAppointment(Appointment appointment) =>
        appointment.ServiceType is "Medical" or "Dental" &&
        appointment.SubService.EndsWith(" Follow-up", StringComparison.OrdinalIgnoreCase);

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
        var today = DateTime.Today;

        return all
            .Where(a =>
                (SlotOccupyingStatuses.Contains(a.Status) && a.VisitDate.Date >= today) ||
                (a.Status == "Reschedule Required" && a.VisitDate.Date >= today) ||
                (a.Status == "Reschedule Requested" &&
                 a.ProposedVisitDate.HasValue &&
                 a.ProposedVisitDate.Value.Date >= today))
            .OrderBy(GetDisplayedVisitDateTime)
            .ToList();
    }

    public async Task<Appointment?> GetNextUpcomingAsync(string patientEmail)
    {
        var all = await GetAllForPatientAsync(patientEmail);
        var today = DateTime.Today;

        return all
            .Where(a =>
                (SlotOccupyingStatuses.Contains(a.Status) && a.VisitDate.Date >= today) ||
                (a.Status == "Reschedule Required" && a.VisitDate.Date >= today) ||
                (a.Status == "Reschedule Requested" &&
                 a.ProposedVisitDate.HasValue &&
                 a.ProposedVisitDate.Value.Date >= today))
            .OrderBy(GetDisplayedVisitDateTime)
            .FirstOrDefault();
    }

    public static DateTime GetVisitDateTime(Appointment appointment)
    {
        if (DateTime.TryParse(appointment.VisitTime, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsedTime))
            return appointment.VisitDate.Date + parsedTime.TimeOfDay;

        return appointment.VisitDate.Date.AddHours(23).AddMinutes(59);
    }

    private static DateTime GetDisplayedVisitDateTime(Appointment appointment)
    {
        if (appointment.Status == "Reschedule Requested" &&
            appointment.ProposedVisitDate.HasValue &&
            DateTime.TryParse(
                appointment.ProposedVisitTime,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var proposedTime))
        {
            return appointment.ProposedVisitDate.Value.Date.Add(proposedTime.TimeOfDay);
        }

        return GetVisitDateTime(appointment);
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
        const int slotMinutes = AppointmentShiftPolicy.AppointmentDurationMinutes;
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

    /// <summary>
    /// Checks whether one exact minute-level appointment start can currently be requested.
    /// BookAsync and the rescheduling/approval methods repeat the checks transactionally
    /// when saving, so this result is for timely UI feedback rather than final authorization.
    /// </summary>
    public async Task<string?> ValidateSlotAvailabilityAsync(
        DateTime date,
        string serviceType,
        string subService,
        string timeText,
        string? patientEmail = null,
        int? ignoreAppointmentId = null)
    {
        var scheduleError = await ClinicScheduleService.Instance.ValidateSlotAsync(
            date, serviceType, subService, timeText);
        if (scheduleError != null)
            return scheduleError;

        if (!AppointmentShiftPolicy.TryParseTime(timeText, out var time))
            return "Please select a valid appointment time.";

        await EnsureInitializedAsync();
        var clinicalType = GetClinicalType(new Appointment
        {
            ServiceType = serviceType,
            SubService = subService
        });
        var requiredRole = GetClinicalStaffRole(clinicalType);
        var startsAt = date.Date.Add(time);
        var endsAt = startsAt.AddMinutes(AppointmentShiftPolicy.AppointmentDurationMinutes);
        string? availabilityError = null;

        await _db.RunInTransactionAsync(connection =>
        {
            var sameDay = connection.Table<Appointment>()
                .ToList()
                .Where(existing => existing.VisitDate.Date == date.Date)
                .ToList();

            if (!string.IsNullOrWhiteSpace(patientEmail) && sameDay.Any(existing =>
                    existing.Id != ignoreAppointmentId &&
                    existing.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
                    SlotOccupyingStatuses.Contains(existing.Status) &&
                    GetVisitDateTime(existing) < endsAt &&
                    GetVisitDateTime(existing).AddMinutes(AppointmentShiftPolicy.AppointmentDurationMinutes) > startsAt))
            {
                availabilityError = "You already have an appointment during that time. Choose another time.";
                return;
            }

            if (requiredRole != null && !StaffScheduleService.FindAvailableStaffId(
                    connection,
                    requiredRole,
                    startsAt,
                    endsAt,
                    AppointmentShiftPolicy.AppointmentDurationMinutes,
                    ignoreAppointmentId ?? 0).HasValue)
            {
                availabilityError = $"No active {requiredRole.ToLowerInvariant()} is scheduled and available at that time. Choose another time or ask the Clinic Administrator to update staff schedules.";
            }
        });

        return availabilityError;
    }

    public async Task<List<Appointment>> GetPendingApprovalsAsync()
    {
        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        var all = await _db.Table<Appointment>().ToListAsync();
        return all.Where(a => a.Status == "Pending" && !IsFollowUpAppointment(a))
            .OrderBy(a => a.VisitDate)
            .ToList();
    }

    public async Task<bool> ApproveFollowUpAsync(int appointmentId)
    {
        var reviewer = AuthService.Instance.CurrentUser;
        if (reviewer == null)
            return false;

        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();
        var requested = await GetByIdAsync(appointmentId);
        if (requested == null || !IsFollowUpAppointment(requested))
            return false;
        var scheduleError = await ClinicScheduleService.Instance.ValidateSlotAsync(
            requested.VisitDate, requested.ServiceType, requested.SubService, requested.VisitTime);
        if (scheduleError != null)
            return false;

        var requiredShift = AppointmentShiftPolicy.GetShiftForAppointmentStart(
            AppointmentShiftPolicy.TryParseTime(requested.VisitTime, out var requestedTime)
                ? requestedTime
                : TimeSpan.MinValue);
        if (requiredShift == null ||
            !string.Equals(requested.AssignedShift, requiredShift, StringComparison.OrdinalIgnoreCase))
            return false;

        const int slotMinutes = AppointmentShiftPolicy.AppointmentDurationMinutes;
        Appointment? approved = null;

        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null || appointment.Status != "Pending" || !IsFollowUpAppointment(appointment))
                return;

            if (!string.Equals(appointment.AssignedShift, requiredShift, StringComparison.OrdinalIgnoreCase))
                return;

            var requiredRole = GetClinicalStaffRole(GetClinicalType(appointment));
            if (requiredRole == null || !StaffScheduleService.IsActiveClinicStaff(connection, reviewer.Id, requiredRole))
                return;

            var startsAt = GetVisitDateTime(appointment);
            if (!StaffScheduleService.IsStaffAvailable(
                    connection, reviewer.Id, requiredRole, startsAt,
                    startsAt.AddMinutes(slotMinutes), slotMinutes, appointment.Id))
            {
                return;
            }

            appointment.AssignedStaffId = reviewer.Id;
            appointment.ReviewingStaffId = reviewer.Id;
            appointment.Status = "Confirmed";
            appointment.SuggestedScheduleStatus = string.Empty;
            connection.Update(appointment);
            approved = appointment;
        });

        if (approved == null)
            return false;

        await NotificationService.Instance.AddAsync(
            approved.PatientEmail,
            "Follow-Up Appointment Approved",
            $"Your {approved.ServiceType.ToLowerInvariant()} follow-up on {approved.VisitDate:MMM dd, yyyy} at {approved.VisitTime} was approved.");
        await NotifyAssignedClinicalStaffAsync(
            approved,
            "Follow-Up Appointment Confirmed",
            $"You are assigned to {approved.PatientEmail}'s follow-up on {approved.VisitDate:MMM dd, yyyy} at {approved.VisitTime}.");
        return true;
    }

    public async Task<bool> DeclineFollowUpAsync(int appointmentId, string reason)
    {
        var reviewer = AuthService.Instance.CurrentUser;
        if (reviewer == null || string.IsNullOrWhiteSpace(reason))
            return false;

        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();
        Appointment? declined = null;

        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null || appointment.Status != "Pending" || !IsFollowUpAppointment(appointment))
                return;

            var requiredRole = GetClinicalStaffRole(GetClinicalType(appointment));
            if (requiredRole == null ||
                !StaffScheduleService.IsActiveClinicStaff(connection, reviewer.Id, requiredRole))
            {
                return;
            }

            appointment.ReviewingStaffId = reviewer.Id;
            appointment.ReviewRemarks = reason.Trim();
            appointment.Status = "Declined";
            connection.Update(appointment);
            declined = appointment;
        });

        if (declined == null)
            return false;

        await NotificationService.Instance.AddAsync(
            declined.PatientEmail,
            "Follow-Up Request Declined",
            $"Your {declined.ServiceType.ToLowerInvariant()} follow-up request for {declined.VisitDate:MMM dd, yyyy} was declined. Reason: {declined.ReviewRemarks}");
        await NotifyAssignedClinicalStaffAsync(
            declined,
            "Follow-Up Request Declined",
            $"The follow-up request for {declined.PatientEmail} was declined.");
        return true;
    }

    public async Task<AppointmentBookingResult> SuggestFollowUpScheduleAsync(
        int appointmentId,
        DateTime proposedDate,
        string proposedTime,
        string remarks)
    {
        var reviewer = AuthService.Instance.CurrentUser;
        if (reviewer == null)
            return new(false, "Please sign in as the assigned Doctor or Dentist.");

        var appointment = await GetByIdAsync(appointmentId);
        if (appointment == null || appointment.Status != "Pending" || !IsFollowUpAppointment(appointment))
            return new(false, "This follow-up request is no longer waiting for review.");

        var requiredRole = GetClinicalStaffRole(GetClinicalType(appointment));
        if (requiredRole == null || !string.Equals(reviewer.Role, requiredRole, StringComparison.OrdinalIgnoreCase))
            return new(false, "Only the Doctor or Dentist for this follow-up type can suggest a schedule.");

        var scheduleError = await ClinicScheduleService.Instance.ValidateSlotAsync(
            proposedDate, appointment.ServiceType, appointment.SubService, proposedTime);
        if (scheduleError != null)
            return new(false, scheduleError);

        if (!DateTime.TryParse(proposedTime, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsedTime))
            return new(false, "Choose a valid suggested time.");

        if (proposedDate.Date == appointment.VisitDate.Date &&
            parsedTime.TimeOfDay == GetVisitDateTime(appointment).TimeOfDay)
        {
            return new(false, "Choose a different date or time from the requested schedule.");
        }

        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();
        const int slotMinutes = AppointmentShiftPolicy.AppointmentDurationMinutes;
        var proposedStartsAt = proposedDate.Date.Add(parsedTime.TimeOfDay);
        string? failure = null;
        Appointment? updated = null;

        await _db.RunInTransactionAsync(connection =>
        {
            var current = connection.Find<Appointment>(appointmentId);
            if (current == null || current.Status != "Pending" || !IsFollowUpAppointment(current))
            {
                failure = "This follow-up request has already changed.";
                return;
            }

            if (!StaffScheduleService.IsActiveClinicStaff(connection, reviewer.Id, requiredRole))
            {
                failure = "Your clinic staff account is not active.";
                return;
            }

            var availableStaff = StaffScheduleService.FindAvailableStaffId(
                connection, requiredRole, proposedStartsAt,
                proposedStartsAt.AddMinutes(slotMinutes), slotMinutes, current.Id);
            if (!availableStaff.HasValue)
            {
                failure = $"No active {requiredRole.ToLowerInvariant()} is available at the suggested time.";
                return;
            }

            var dayStart = proposedDate.Date;
            var dayEnd = dayStart.AddDays(1);
            var patientConflict = connection.Table<Appointment>().ToList().Any(existing =>
                existing.Id != current.Id &&
                existing.PatientEmail.Equals(current.PatientEmail, StringComparison.OrdinalIgnoreCase) &&
                existing.VisitDate.Date == dayStart &&
                SlotOccupyingStatuses.Contains(existing.Status) &&
                GetVisitDateTime(existing) < proposedStartsAt.AddMinutes(slotMinutes) &&
                GetVisitDateTime(existing).AddMinutes(slotMinutes) > proposedStartsAt);
            if (patientConflict)
            {
                failure = "The patient already has an appointment during that suggested time.";
                return;
            }

            current.ProposedVisitDate = dayStart;
            current.ProposedVisitTime = parsedTime.ToString("h:mm tt", CultureInfo.CurrentCulture);
            current.ReviewingStaffId = reviewer.Id;
            current.ReviewRemarks = remarks?.Trim() ?? string.Empty;
            current.SuggestedScheduleStatus = "Awaiting Patient";
            current.Status = "Reschedule Requested";
            connection.Update(current);
            updated = current;
        });

        if (updated == null)
            return new(false, failure ?? "The suggested schedule could not be saved.");

        var note = string.IsNullOrWhiteSpace(updated.ReviewRemarks) ? string.Empty : $" Note: {updated.ReviewRemarks}";
        await NotificationService.Instance.AddAsync(
            updated.PatientEmail,
            "Follow-Up Schedule Suggested",
            $"The clinic suggested {updated.ProposedVisitDate:MMM dd, yyyy} at {updated.ProposedVisitTime} for your follow-up. Open My Appointments to accept or decline.{note}");
        await NotifyAssignedClinicalStaffAsync(
            updated,
            "Follow-Up Schedule Suggested",
            $"An alternative follow-up schedule was sent to {updated.PatientEmail}.");
        return new(true, "The suggested schedule was sent to the patient.");
    }

    public async Task<bool> AcceptSuggestedFollowUpScheduleAsync(int appointmentId)
    {
        var patient = AuthService.Instance.CurrentUser;
        if (patient == null || !IsPatientRole(patient.Role))
            return false;

        var appointment = await GetByIdAsync(appointmentId);
        if (appointment == null ||
            appointment.Status != "Reschedule Requested" ||
            !appointment.PatientEmail.Equals(patient.Email, StringComparison.OrdinalIgnoreCase) ||
            !appointment.ProposedVisitDate.HasValue ||
            string.IsNullOrWhiteSpace(appointment.ProposedVisitTime))
        {
            return false;
        }

        var proposedDate = appointment.ProposedVisitDate.Value;
        var scheduleError = await ClinicScheduleService.Instance.ValidateSlotAsync(
            proposedDate, appointment.ServiceType, appointment.SubService, appointment.ProposedVisitTime);
        if (scheduleError != null)
            return false;
        if (!DateTime.TryParse(
                appointment.ProposedVisitTime,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var proposedTime))
        {
            return false;
        }

        await EnsureInitializedAsync();
        var requiredRole = GetClinicalStaffRole(GetClinicalType(appointment));
        if (requiredRole == null)
            return false;

        const int slotMinutes = AppointmentShiftPolicy.AppointmentDurationMinutes;
        var proposedStartsAt = proposedDate.Date.Add(proposedTime.TimeOfDay);
        Appointment? accepted = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var current = connection.Find<Appointment>(appointmentId);
            if (current == null ||
                current.Status != "Reschedule Requested" ||
                !current.PatientEmail.Equals(patient.Email, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var availableStaffId = StaffScheduleService.FindAvailableStaffId(
                connection, requiredRole, proposedStartsAt,
                proposedStartsAt.AddMinutes(slotMinutes), slotMinutes, current.Id);
            if (!availableStaffId.HasValue)
                return;

            var dayStart = proposedDate.Date;
            var dayEnd = dayStart.AddDays(1);
            var sameDayAppointments = connection.Table<Appointment>()
                .Where(existing => existing.VisitDate >= dayStart && existing.VisitDate < dayEnd)
                .ToList();
            var patientConflict = sameDayAppointments.Any(existing =>
                existing.Id != current.Id &&
                existing.PatientEmail.Equals(current.PatientEmail, StringComparison.OrdinalIgnoreCase) &&
                SlotOccupyingStatuses.Contains(existing.Status) &&
                GetVisitDateTime(existing) < proposedStartsAt.AddMinutes(slotMinutes) &&
                GetVisitDateTime(existing).AddMinutes(slotMinutes) > proposedStartsAt);
            if (patientConflict)
                return;

            current.VisitDate = dayStart;
            current.VisitTime = proposedTime.ToString("h:mm tt", CultureInfo.CurrentCulture);
            current.AssignedShift = AppointmentShiftPolicy.GetShiftForAppointmentStart(proposedTime.TimeOfDay) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(current.AssignedShift))
                return;
            current.AssignedStaffId = availableStaffId.Value;
            current.Status = "Confirmed";
            current.SuggestedScheduleStatus = "Accepted";
            connection.Update(current);
            accepted = current;
        });

        if (accepted == null)
            return false;

        await NotificationService.Instance.AddAsync(
            accepted.PatientEmail,
            "Follow-Up Schedule Accepted",
            $"Your follow-up appointment is confirmed for {accepted.VisitDate:MMM dd, yyyy} at {accepted.VisitTime}.");
        await NotifyAssignedClinicalStaffAsync(
            accepted,
            "Follow-Up Schedule Accepted",
            $"{accepted.PatientEmail} accepted the suggested follow-up schedule for {accepted.VisitDate:MMM dd, yyyy} at {accepted.VisitTime}.");
        return true;
    }

    public async Task<bool> DeclineSuggestedFollowUpScheduleAsync(int appointmentId)
    {
        var patient = AuthService.Instance.CurrentUser;
        if (patient == null || !IsPatientRole(patient.Role))
            return false;

        await EnsureInitializedAsync();
        Appointment? declined = null;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.Status != "Reschedule Requested" ||
                !appointment.PatientEmail.Equals(patient.Email, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            appointment.Status = "Cancelled";
            appointment.SuggestedScheduleStatus = "Declined";
            connection.Update(appointment);
            declined = appointment;
        });

        if (declined == null)
            return false;

        await NotificationService.Instance.AddAsync(
            declined.PatientEmail,
            "Suggested Follow-Up Declined",
            "You declined the suggested follow-up schedule. Submit a new request if you still need an appointment.");
        await NotifyAssignedClinicalStaffAsync(
            declined,
            "Suggested Follow-Up Declined",
            $"{declined.PatientEmail} declined the suggested follow-up schedule.");
        return true;
    }

    private static bool IsPatientRole(string role) =>
        role is "Student" or "Faculty" or "Admin Personnel" or "Non-Teaching";

    private static bool CanRequestRescheduleFrom(string status) =>
        status is "Reschedule Required" or "Missed" or "Declined" or "Expired";

    private static bool IsActiveAppointmentStatus(string status) =>
        SlotOccupyingStatuses.Contains(status) || status is "Reschedule Requested" or "Reschedule Required";

    public async Task<bool> ApproveAsync(int appointmentId)
    {
        if (AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
            return false;

        await EnsureInitializedAsync();
        await ExpireElapsedAppointmentsAsync();

        var requestedAppointment = await GetByIdAsync(appointmentId);
        if (requestedAppointment == null ||
            requestedAppointment.Status != "Pending" ||
            IsFollowUpAppointment(requestedAppointment))
            return false;

        var scheduleError = await ClinicScheduleService.Instance.ValidateSlotAsync(
            requestedAppointment.VisitDate,
            requestedAppointment.ServiceType,
            requestedAppointment.SubService,
            requestedAppointment.VisitTime);
        if (scheduleError != null ||
            !AppointmentShiftPolicy.TryParseTime(requestedAppointment.VisitTime, out var requestedTime))
            return false;

        var requiredShift = AppointmentShiftPolicy.GetShiftForAppointmentStart(requestedTime);
        if (requiredShift == null ||
            !string.Equals(requestedAppointment.AssignedShift, requiredShift, StringComparison.OrdinalIgnoreCase))
            return false;

        Appointment? approvedAppointment = null;
        const int slotMinutes = AppointmentShiftPolicy.AppointmentDurationMinutes;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null ||
                appointment.Status != "Pending" ||
                IsFollowUpAppointment(appointment))
                return;

            if (!string.Equals(appointment.AssignedShift, requiredShift, StringComparison.OrdinalIgnoreCase))
                return;

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
                IsFollowUpAppointment(appointment))
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
    public async Task<AppointmentBookingResult> RequestRescheduleAsync(
        int originalAppointmentId,
        DateTime selectedDate,
        string selectedTime,
        string? patientNote = null)
    {
        var patient = AuthService.Instance.CurrentUser;
        if (patient == null || !IsPatientRole(patient.Role))
            return new(false, "Sign in to the patient account to request a new schedule.");

        await EnsureInitializedAsync();
        var original = await GetByIdAsync(originalAppointmentId);
        if (original == null ||
            !original.PatientEmail.Equals(patient.Email, StringComparison.OrdinalIgnoreCase))
            return new(false, "This appointment does not belong to the signed-in patient.");

        if (!CanRequestRescheduleFrom(original.Status))
            return new(false, "This appointment is not waiting for a new schedule.");

        var scheduleError = await ClinicScheduleService.Instance.ValidateSlotAsync(
            selectedDate, original.ServiceType, original.SubService, selectedTime);
        if (scheduleError != null)
            return new(false, scheduleError);

        if (!AppointmentShiftPolicy.TryParseTime(selectedTime, out var parsedTime))
            return new(false, "Choose a valid appointment time.");

        var assignedShift = AppointmentShiftPolicy.GetShiftForAppointmentStart(parsedTime);
        if (assignedShift == null)
            return new(false, AppointmentShiftPolicy.GetSlotPolicyError(parsedTime));

        var visitDate = selectedDate.Date;
        var now = DateTime.Now;
        var reason = original.ReschedulingReason;
        if (!string.IsNullOrWhiteSpace(patientNote))
            reason = string.IsNullOrWhiteSpace(reason)
                ? patientNote.Trim()
                : $"{reason} Patient note: {patientNote.Trim()}";
        if (string.IsNullOrWhiteSpace(reason))
            reason = "The patient requested a new appointment schedule.";

        var replacement = new Appointment
        {
            PatientEmail = original.PatientEmail,
            ServiceType = original.ServiceType,
            SubService = original.SubService,
            ReasonOrPurpose = original.ReasonOrPurpose,
            OriginalAppointmentId = original.OriginalAppointmentId,
            RecommendedByStaffId = original.RecommendedByStaffId,
            RecommendedFollowUpDate = original.RecommendedFollowUpDate,
            FollowUpInstructions = original.FollowUpInstructions,
            VisitDate = visitDate,
            VisitTime = parsedTime.ToString("h:mm tt", CultureInfo.CurrentCulture),
            AssignedShift = assignedShift,
            RescheduledFromAppointmentId = original.Id,
            ReschedulingReason = reason,
            RescheduledAt = now,
            Location = original.Location,
            Status = "Pending",
            CertificateStatus = original.ServiceType == "Cert Request"
                ? "Awaiting Appointment Approval"
                : string.Empty
        };

        var requiredRole = GetClinicalStaffRole(GetClinicalType(replacement));
        var visitStartsAt = visitDate.Add(parsedTime);
        var visitEndsAt = visitStartsAt.AddMinutes(AppointmentShiftPolicy.AppointmentDurationMinutes);
        string? failure = null;
        Appointment? savedReplacement = null;

        await _db.RunInTransactionAsync(connection =>
        {
            var currentOriginal = connection.Find<Appointment>(originalAppointmentId);
            if (currentOriginal == null ||
                !currentOriginal.PatientEmail.Equals(patient.Email, StringComparison.OrdinalIgnoreCase) ||
                !CanRequestRescheduleFrom(currentOriginal.Status))
            {
                failure = "This appointment changed. Refresh your appointments and try again.";
                return;
            }

            var allAppointments = connection.Table<Appointment>().ToList();
            if (allAppointments.Any(existing =>
                    existing.RescheduledFromAppointmentId == currentOriginal.Id &&
                    IsActiveAppointmentStatus(existing.Status)))
            {
                failure = "A new schedule request is already active for this appointment. Open My Appointments to review it.";
                return;
            }

            var dayStart = visitDate.Date;
            var dayEnd = dayStart.AddDays(1);
            var sameDayAppointments = allAppointments
                .Where(existing => existing.VisitDate >= dayStart && existing.VisitDate < dayEnd)
                .ToList();
            var patientConflict = sameDayAppointments.Any(existing =>
                existing.PatientEmail.Equals(patient.Email, StringComparison.OrdinalIgnoreCase) &&
                SlotOccupyingStatuses.Contains(existing.Status) &&
                GetVisitDateTime(existing) < visitEndsAt &&
                GetVisitDateTime(existing).AddMinutes(AppointmentShiftPolicy.AppointmentDurationMinutes) > visitStartsAt);
            if (patientConflict)
            {
                failure = "You already have an appointment during that time. Choose another available slot.";
                return;
            }

            if (IsFollowUpAppointment(replacement) &&
                allAppointments.Any(existing =>
                    existing.Id != currentOriginal.Id &&
                    existing.OriginalAppointmentId == replacement.OriginalAppointmentId &&
                    ActiveFollowUpRequestStatuses.Contains(existing.Status)))
            {
                failure = "An active follow-up request already exists for that previous visit. Open My Appointments to review it.";
                return;
            }

            if (requiredRole != null)
            {
                int? availableStaffId = null;
                var preferredStaffIds = new[]
                    {
                        currentOriginal.AssignedStaffId,
                        currentOriginal.RecommendedByStaffId
                    }
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();

                foreach (var staffId in preferredStaffIds)
                {
                    if (!StaffScheduleService.IsStaffAvailable(
                            connection, staffId, requiredRole, visitStartsAt, visitEndsAt,
                            AppointmentShiftPolicy.AppointmentDurationMinutes))
                        continue;

                    availableStaffId = staffId;
                    break;
                }

                availableStaffId ??= StaffScheduleService.FindAvailableStaffId(
                    connection,
                    requiredRole,
                    visitStartsAt,
                    visitEndsAt,
                    AppointmentShiftPolicy.AppointmentDurationMinutes);

                if (!availableStaffId.HasValue)
                {
                    failure = $"No active {requiredRole.ToLowerInvariant()} is available at that time. Choose another slot.";
                    return;
                }

                replacement.AssignedStaffId = availableStaffId.Value;
            }

            connection.Insert(replacement);
            currentOriginal.Status = "Rescheduled";
            currentOriginal.RescheduledToAppointmentId = replacement.Id;
            currentOriginal.RescheduledAt = now;
            if (string.IsNullOrWhiteSpace(currentOriginal.ReschedulingReason))
                currentOriginal.ReschedulingReason = reason;
            connection.Update(currentOriginal);
            savedReplacement = replacement;
        });

        if (savedReplacement == null)
            return new(false, failure ?? "The new schedule could not be saved.");

        try
        {
            await NotificationService.Instance.AddAsync(
                savedReplacement.PatientEmail,
                "Reschedule Request Submitted",
                $"Your new {savedReplacement.ServiceType} appointment request for {savedReplacement.VisitDate:MMM dd, yyyy} at {savedReplacement.VisitTime} ({savedReplacement.AssignedShift} Shift) is Pending clinic approval.");
            await NotifyAssignedClinicalStaffAsync(
                savedReplacement,
                "Rescheduled Appointment Request",
                $"A rescheduled appointment request for {savedReplacement.PatientEmail} is pending for {savedReplacement.VisitDate:MMM dd, yyyy} at {savedReplacement.VisitTime} ({savedReplacement.AssignedShift} Shift).");
        }
        catch
        {
            // The saved appointment remains valid if a local notification cannot be created.
        }

        return new(true,
            $"Your new schedule request was submitted for clinic approval. Assigned shift: {savedReplacement.AssignedShift}.");
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

        return all
            .Where(a =>
                a.PatientEmail.Equals(patientEmail, StringComparison.OrdinalIgnoreCase) &&
                a.Status is "Confirmed" or "Reschedule Required")
            .OrderBy(a => a.Status == "Confirmed" ? 0 : 1)
            .ThenBy(GetVisitDateTime)
            .FirstOrDefault();
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

    public async Task<AppointmentCheckInResult> GetCheckInEligibilityAsync(int appointmentId)
    {
        if (AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
            return new(false, null, "Only R.E.A.D.S. Scholars can check in patients.");
        await EnsureInitializedAsync();
        Appointment? evaluatedAppointment = null;
        CheckInPolicyDecision? evaluatedDecision = null;
        var rescheduleRequiredNow = false;
        var now = DateTime.Now;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null)
                return;
            if (appointment.VisitDate.Date != now.Date)
            {
                evaluatedAppointment = appointment;
                evaluatedDecision = new(false, false, false, "Check-in is available only on the appointment date.");
                return;
            }
            if (appointment.Status is not ("Confirmed" or "Reschedule Required"))
            {
                evaluatedAppointment = appointment;
                evaluatedDecision = new(false, false, false, "This appointment is no longer eligible for check-in.");
                return;
            }
            var decision = AppointmentShiftPolicy.EvaluateCheckIn(
                appointment.AssignedShift,
                GetVisitDateTime(appointment),
                now);
            evaluatedAppointment = appointment;
            if (appointment.Status == "Confirmed" && decision.RequiresReschedule)
            {
                appointment.Status = "Reschedule Required";
                appointment.ReschedulingReason = decision.Message;
                appointment.RescheduledAt = null;
                if (appointment.ServiceType == "Cert Request")
                    appointment.CertificateStatus = "Reschedule Required";
                connection.Update(appointment);
                rescheduleRequiredNow = true;
            }
            if (appointment.Status == "Reschedule Required")
            {
                var message = decision.RequiresReschedule
                    ? decision.Message
                    : string.IsNullOrWhiteSpace(appointment.ReschedulingReason)
                        ? "This appointment needs a new schedule. Please request a new appointment time."
                        : appointment.ReschedulingReason;
                evaluatedDecision = new(false, decision.IsLate, true, message);
            }
            else
            {
                evaluatedDecision = decision;
            }
        });
        if (evaluatedAppointment == null || evaluatedDecision == null)
            return new(false, null, "This appointment could not be found.");
        if (rescheduleRequiredNow)
        {
            try
            {
                await NotificationService.Instance.AddAsync(
                    evaluatedAppointment.PatientEmail,
                    "Reschedule Required",
                    $"{evaluatedDecision.Message} Open My Appointments to choose a new date and time. The new request must be approved by the clinic.");
            }
            catch
            {
                // The reschedule status is authoritative even if local notification creation fails.
            }
        }
        return new(
            evaluatedDecision.CanCheckIn && evaluatedAppointment.Status == "Confirmed",
            evaluatedAppointment,
            evaluatedDecision.Message,
            evaluatedDecision.IsLate,
            evaluatedDecision.RequiresReschedule);
    }
    public async Task<AppointmentCheckInResult> CheckInWithPolicyAsync(int appointmentId)
    {
        var readiness = await GetCheckInEligibilityAsync(appointmentId);
        if (!readiness.Success || readiness.Appointment == null)
            return readiness;
        Appointment? checkedInAppointment = null;
        Appointment? appointmentNeedingReschedule = null;
        CheckInPolicyDecision? finalDecision = null;
        var now = DateTime.Now;
        await _db.RunInTransactionAsync(connection =>
        {
            var appointment = connection.Find<Appointment>(appointmentId);
            if (appointment == null || appointment.Status != "Confirmed")
            {
                finalDecision = new(
                    false,
                    false,
                    appointment?.Status == "Reschedule Required",
                    appointment?.ReschedulingReason ?? "This appointment is no longer eligible for check-in.");
                return;
            }
            var decision = AppointmentShiftPolicy.EvaluateCheckIn(
                appointment.AssignedShift,
                GetVisitDateTime(appointment),
                now);
            if (!decision.CanCheckIn)
            {
                finalDecision = decision;
                if (decision.RequiresReschedule)
                {
                    appointment.Status = "Reschedule Required";
                    appointment.ReschedulingReason = decision.Message;
                    appointment.RescheduledAt = null;
                    if (appointment.ServiceType == "Cert Request")
                        appointment.CertificateStatus = "Reschedule Required";
                    connection.Update(appointment);
                    appointmentNeedingReschedule = appointment;
                }
                return;
            }
            var today = now.Date;
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
            finalDecision = decision;
        });
        if (appointmentNeedingReschedule != null && finalDecision != null)
        {
            try
            {
                await NotificationService.Instance.AddAsync(
                    appointmentNeedingReschedule.PatientEmail,
                    "Reschedule Required",
                    $"{finalDecision.Message} Open My Appointments to choose a new date and time. The new request must be approved by the clinic.");
            }
            catch
            {
                // The reschedule status is authoritative even if local notification creation fails.
            }
        }
        if (checkedInAppointment == null || finalDecision == null)
        {
            var failureMessage = finalDecision?.Message ?? "This appointment is no longer eligible for check-in.";
            return new(
                false,
                appointmentNeedingReschedule ?? readiness.Appointment,
                failureMessage,
                finalDecision?.IsLate ?? false,
                finalDecision?.RequiresReschedule ?? false);
        }
        try
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
        catch
        {
            // The queue assignment succeeds even if a staff notification cannot be created.
        }
        return new(true, checkedInAppointment, finalDecision.Message, finalDecision.IsLate);
    }
    // Retained for existing callers; all check-in paths use the strict policy above.
    public async Task<Appointment?> CheckInAsync(int appointmentId)
    {
        var result = await CheckInWithPolicyAsync(appointmentId);
        return result.Success ? result.Appointment : null;
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
            .Where(a => a.Status == "Pending" || a.Status == "Confirmed" || a.Status == "Reschedule Requested")
            .ToListAsync();
        foreach (var appointment in active)
        {
            if (appointment.Status == "Confirmed")
            {
                if (appointment.VisitDate.Date < now.Date)
                {
                    appointment.Status = "Missed";
                    appointment.ReschedulingReason = "The appointment date passed without check-in. Request a new appointment schedule if you still need care.";
                    if (appointment.ServiceType == "Cert Request")
                        appointment.CertificateStatus = "Missed Appointment";
                    await _db.UpdateAsync(appointment);
                    continue;
                }
                if (appointment.VisitDate.Date > now.Date)
                    continue;
                var decision = AppointmentShiftPolicy.EvaluateCheckIn(
                    appointment.AssignedShift,
                    GetVisitDateTime(appointment),
                    now);
                if (!decision.RequiresReschedule)
                    continue;
                appointment.Status = "Reschedule Required";
                appointment.ReschedulingReason = decision.Message;
                appointment.RescheduledAt = null;
                if (appointment.ServiceType == "Cert Request")
                    appointment.CertificateStatus = "Reschedule Required";
                await _db.UpdateAsync(appointment);
                try
                {
                    await NotificationService.Instance.AddAsync(
                        appointment.PatientEmail,
                        "Reschedule Required",
                        $"{decision.Message} Open My Appointments to choose a new date and time. The new request must be approved by the clinic.");
                }
                catch
                {
                    // Status remains saved even if local notification creation fails.
                }
                continue;
            }
            var effectiveDate = appointment.Status == "Reschedule Requested" && appointment.ProposedVisitDate.HasValue
                ? appointment.ProposedVisitDate.Value.Date
                : appointment.VisitDate.Date;
            if (effectiveDate > now.Date)
                continue;
            var effectiveTimeText = appointment.Status == "Reschedule Requested" &&
                                    !string.IsNullOrWhiteSpace(appointment.ProposedVisitTime)
                ? appointment.ProposedVisitTime
                : appointment.VisitTime;
            var shouldExpire = effectiveDate < now.Date;
            if (!shouldExpire)
            {
                if (!AppointmentShiftPolicy.TryParseTime(effectiveTimeText, out var timeOfDay))
                {
                    shouldExpire = true;
                }
                else
                {
                    var shift = AppointmentShiftPolicy.GetShiftForAppointmentStart(timeOfDay);
                    if (shift == null)
                    {
                        shouldExpire = true;
                    }
                    else
                    {
                        var shiftEnd = shift == AppointmentShiftPolicy.MorningShift
                            ? AppointmentShiftPolicy.MorningEnd
                            : AppointmentShiftPolicy.AfternoonEnd;
                        shouldExpire = now.TimeOfDay >= shiftEnd;
                    }
                }
            }
            if (!shouldExpire)
                continue;
            appointment.Status = "Expired";
            if (appointment.ServiceType == "Cert Request")
                appointment.CertificateStatus = "Expired";
            await _db.UpdateAsync(appointment);
        }
    }    public async Task<int> GetUnseenCountAsync(string patientEmail)
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
        const int slotMinutes = AppointmentShiftPolicy.AppointmentDurationMinutes;
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
                        a.Status != "Missed" && a.Status != "Reschedule Requested")
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
