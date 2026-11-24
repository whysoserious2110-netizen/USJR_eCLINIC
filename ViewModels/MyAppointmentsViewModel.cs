using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.ViewModels;

public partial class AppointmentListItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string SubService { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public DateTime VisitDateTime { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string AssignedShiftDisplay { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ReasonOrPurpose { get; set; } = string.Empty;
    public bool HasReasonOrPurpose => !string.IsNullOrWhiteSpace(ReasonOrPurpose);
    public bool IsFollowUp { get; set; }
    public string PreviousVisitDisplay { get; set; } = string.Empty;
    public bool HasPreviousVisitDisplay => !string.IsNullOrWhiteSpace(PreviousVisitDisplay);
    public string FollowUpInstructions { get; set; } = string.Empty;
    public bool HasFollowUpInstructions => !string.IsNullOrWhiteSpace(FollowUpInstructions);
    public string ReviewRemarks { get; set; } = string.Empty;
    public bool HasReviewRemarks => !string.IsNullOrWhiteSpace(ReviewRemarks);
    public string ProposedDateDisplay { get; set; } = string.Empty;
    public string ProposedTimeDisplay { get; set; } = string.Empty;
    public bool CanRespondToSuggestedSchedule =>
        Status == "Reschedule Requested" &&
        !string.IsNullOrWhiteSpace(ProposedDateDisplay) &&
        !string.IsNullOrWhiteSpace(ProposedTimeDisplay);
    public string ProposedScheduleDisplay =>
        CanRespondToSuggestedSchedule
            ? $"Suggested: {ProposedDateDisplay} at {ProposedTimeDisplay}"
            : string.Empty;
    public string DetailTitle => string.IsNullOrWhiteSpace(SubService) ? ServiceType : SubService;
    public bool IsCertificateRequest { get; set; }
    public string CertificateStatus { get; set; } = string.Empty;
    public string CertificateDecisionNote { get; set; } = string.Empty;
    public bool HasCertificateDecisionNote => !string.IsNullOrWhiteSpace(CertificateDecisionNote);
    public string CertificateTrackingMessage => CertificateStatus switch
    {
        "Awaiting Appointment Approval" => "R.E.A.D.S. staff are reviewing the appointment date and time.",
        "Awaiting Clinic Visit" => "Your appointment is confirmed. Check in by 11:00 AM for a Morning Shift appointment or by 6:00 PM for an Afternoon Shift appointment.",
        "Reschedule Required" => "The check-in window for this certificate appointment has passed. Request a new schedule; clinic approval is required.",
        "Visit In Progress" => "Your clinic visit is in progress. The responsible clinician will review the request.",
        "Awaiting Clinical Review" => "Your visit is complete. The responsible clinician is reviewing the certificate request.",
        "Ready for Pickup" => "Your paper certificate is ready. Visit the clinic to collect it.",
        "Declined" => "The clinician declined the request. Review the note below or contact the clinic.",
        "Collected" => "The paper certificate has been marked as collected.",
        "Expired" => "The request expired because the clinic day ended before it was approved.",
        "Missed Appointment" => "The confirmed appointment passed without check-in. Submit a new request if you still need the certificate.",
        "Cancelled" => "The certificate appointment was cancelled.",
        _ => "Certificate request status is not available."
    };
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay => Status == "Reschedule Requested"
        ? "Schedule response needed"
        : Status == "Reschedule Required"
            ? "Reschedule Required"
        : Status;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanCancel { get; set; }
    public bool CanReschedule => Status is "Reschedule Required" or "Missed" or "Declined" or "Expired";
    public string ReschedulingReason { get; set; } = string.Empty;
    public DateTime? RescheduledAt { get; set; }
    public int? RescheduledFromAppointmentId { get; set; }
    public int? RescheduledToAppointmentId { get; set; }
    public bool HasRescheduleHistory =>
        !string.IsNullOrWhiteSpace(ReschedulingReason) ||
        RescheduledFromAppointmentId.HasValue || RescheduledToAppointmentId.HasValue;
    public string RescheduleHistoryDisplay
    {
        get
        {
            var dateText = RescheduledAt.HasValue
                ? $" Reschedule date: {RescheduledAt.Value:MMM d, yyyy h:mm tt}."
                : string.Empty;
            var linkText = RescheduledFromAppointmentId.HasValue
                ? $"New request from appointment #{RescheduledFromAppointmentId.Value}."
                : RescheduledToAppointmentId.HasValue
                    ? $"New appointment request #{RescheduledToAppointmentId.Value} was submitted."
                    : string.Empty;
            var reasonText = string.IsNullOrWhiteSpace(ReschedulingReason)
                ? string.Empty
                : $"Reason: {ReschedulingReason}";
            return string.Join(" ", new[] { linkText, reasonText, dateText }.Where(text => !string.IsNullOrWhiteSpace(text)));
        }
    }
    public bool HasElapsedNotice =>
        Status is "Expired" or "Missed" or "Declined" or "Reschedule Required";

    public string ElapsedNotice => Status switch
    {
        "Reschedule Required" => "Your check-in window has passed. Choose another date and time; clinic approval is required.",
        "Expired" => "This request expired because the clinic day ended before it was approved. Submit a new request for another date.",
        "Declined" => "The request was declined. Check Notifications for the reason and submit another request if needed.",
        _ => "The clinic day ended before check-in. This visit is marked missed and cannot be checked in."
    };
}

public partial class AppointmentsViewModel : ObservableObject
{
    private List<AppointmentListItem> _allAppointments = new();
    private int? _appointmentIdToOpen;

    public AppointmentsViewModel(int? appointmentIdToOpen = null)
    {
        _appointmentIdToOpen = appointmentIdToOpen;
    }

    public ObservableCollection<AppointmentListItem> Appointments { get; } = new();

    [ObservableProperty]
    private bool hasAppointments;

    [ObservableProperty]
    private string selectedTab = "Upcoming";

    [ObservableProperty]
    private bool isDetailVisible;

    [ObservableProperty]
    private AppointmentListItem? selectedAppointment;

    public bool IsAllTab => SelectedTab == "All";
    public bool IsUpcomingTab => SelectedTab == "Upcoming";
    public bool IsPastTab => SelectedTab == "Past";
    public string HeaderTitle => IsDetailVisible ? "Appointment Details" : "My Appointments";

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var appts = await Services.AppointmentService.Instance.GetAllForPatientAsync(user.Email);

        _allAppointments = appts.Select(a => new AppointmentListItem
        {
            AppointmentId = a.Id,
            ServiceType = a.ServiceType == "Cert Request" ? "Certificate Request" : a.ServiceType,
            SubService = a.SubService,
            IsCertificateRequest = a.ServiceType == "Cert Request",
            CertificateStatus = a.CertificateStatus,
            CertificateDecisionNote = a.CertificateDecisionNote,
            VisitDate = GetPatientDisplayVisitDate(a),
            VisitDateTime = GetPatientDisplayVisitDateTime(a),
            DateDisplay = GetPatientDisplayVisitDate(a).ToString("MMM dd, yyyy"),
            TimeDisplay = a.Status == "Reschedule Requested" && !string.IsNullOrWhiteSpace(a.ProposedVisitTime)
                ? a.ProposedVisitTime
                : string.IsNullOrWhiteSpace(a.VisitTime) ? "To be arranged" : a.VisitTime,
            AssignedShiftDisplay = string.IsNullOrWhiteSpace(a.AssignedShift)
                ? "Shift not recorded"
                : $"{a.AssignedShift} Shift",
            Location = a.Location,
            ReasonOrPurpose = a.ReasonOrPurpose,
            IsFollowUp = Services.AppointmentService.IsFollowUpAppointment(a),
            FollowUpInstructions = a.FollowUpInstructions,
            ReviewRemarks = a.ReviewRemarks,
            ProposedDateDisplay = a.ProposedVisitDate?.ToString("MMM dd, yyyy") ?? string.Empty,
            ProposedTimeDisplay = a.ProposedVisitTime,
            Status = a.Status,
            ReschedulingReason = a.ReschedulingReason,
            RescheduledAt = a.RescheduledAt,
            RescheduledFromAppointmentId = a.RescheduledFromAppointmentId,
            RescheduledToAppointmentId = a.RescheduledToAppointmentId,
            CanCancel = a.Status == "Pending" || a.Status == "Confirmed",
            StatusColor = a.Status switch
            {
                "Confirmed" => Color.FromArgb("#0F9B8E"),
                "Completed" => Color.FromArgb("#4A90D9"),
                "Cancelled" => Color.FromArgb("#E05B5B"),
                "Declined" => Color.FromArgb("#B3443D"),
                "Missed" => Color.FromArgb("#C2410C"),
                "Expired" => Color.FromArgb("#7C8794"),
                "Reschedule Required" => Color.FromArgb("#C2410C"),
                "Rescheduled" => Color.FromArgb("#64748B"),
                _ => Color.FromArgb("#D9A441")
            }
        }).ToList();

        foreach (var item in _allAppointments.Where(item => item.IsFollowUp))
        {
            var followUp = appts.FirstOrDefault(appointment => appointment.Id == item.AppointmentId);
            if (followUp?.OriginalAppointmentId is not int originalId)
                continue;

            var previousVisit = await Services.AppointmentService.Instance.GetByIdAsync(originalId);
            if (previousVisit == null)
                continue;

            if (followUp.ServiceType == "Medical")
            {
                var record = await Services.MedicalRecordService.Instance.GetByAppointmentIdAsync(originalId);
                item.PreviousVisitDisplay = record == null
                    ? $"Medical visit · {previousVisit.VisitDate:MMM d, yyyy}"
                    : $"Medical visit · {record.ConsultationDate:MMM d, yyyy}\nDiagnosis: {record.Diagnosis}";
            }
            else
            {
                var record = await Services.DentalRecordService.Instance.GetByAppointmentIdAsync(originalId);
                item.PreviousVisitDisplay = record == null
                    ? $"Dental visit · {previousVisit.VisitDate:MMM d, yyyy}"
                    : $"Dental visit · {record.VisitDate:MMM d, yyyy}\nDiagnosis: {record.Diagnosis}";
            }
        }

        ApplyFilter();

        if (_appointmentIdToOpen.HasValue)
        {
            var requestedAppointmentId = _appointmentIdToOpen.Value;
            _appointmentIdToOpen = null;

            SelectedAppointment = _allAppointments.FirstOrDefault(
                appointment => appointment.AppointmentId == requestedAppointmentId);

            if (SelectedAppointment != null)
            {
                IsDetailVisible = true;
                OnPropertyChanged(nameof(HeaderTitle));
            }
        }
        else if (SelectedAppointment != null)
        {
            SelectedAppointment = _allAppointments.FirstOrDefault(
                appointment => appointment.AppointmentId == SelectedAppointment.AppointmentId);
        }

        await Services.AppointmentService.Instance.MarkAllSeenAsync(user.Email);
    }

    private static DateTime GetPatientDisplayVisitDate(Appointment appointment) =>
        appointment.Status == "Reschedule Requested" && appointment.ProposedVisitDate.HasValue
            ? appointment.ProposedVisitDate.Value.Date
            : appointment.VisitDate.Date;

    private static DateTime GetPatientDisplayVisitDateTime(Appointment appointment)
    {
        if (appointment.Status == "Reschedule Requested" &&
            appointment.ProposedVisitDate.HasValue &&
            DateTime.TryParse(
                appointment.ProposedVisitTime,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.None,
                out var proposedTime))
        {
            return appointment.ProposedVisitDate.Value.Date.Add(proposedTime.TimeOfDay);
        }

        return Services.AppointmentService.GetVisitDateTime(appointment);
    }

    private void ApplyFilter()
    {
        var now = DateTime.Now;

        IEnumerable<AppointmentListItem> filtered = SelectedTab switch
        {
            "Upcoming" => _allAppointments
                .Where(a => a.VisitDate.Date >= now.Date &&
                            a.Status is not ("Cancelled" or "Completed" or "Declined" or "Expired" or "Missed" or "Rescheduled"))
                .OrderBy(a => a.VisitDateTime),
            "Past" => _allAppointments
                .Where(a => a.VisitDate.Date < now.Date ||
                            a.Status is "Completed" or "Cancelled" or "Declined" or "Expired" or "Missed" or "Rescheduled")
                .OrderByDescending(a => a.VisitDateTime),
            _ => _allAppointments.OrderByDescending(a => a.VisitDateTime)
        };

        Appointments.Clear();
        foreach (var item in filtered)
            Appointments.Add(item);

        HasAppointments = Appointments.Count > 0;
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        SelectedTab = tab;
        OnPropertyChanged(nameof(IsAllTab));
        OnPropertyChanged(nameof(IsUpcomingTab));
        OnPropertyChanged(nameof(IsPastTab));
        ApplyFilter();
    }

    [RelayCommand]
    private void OpenAppointment(AppointmentListItem item)
    {
        SelectedAppointment = item;
        IsDetailVisible = true;
        OnPropertyChanged(nameof(HeaderTitle));
    }

    [RelayCommand]
    private async Task CancelAppointment(AppointmentListItem item)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Cancel Appointment?",
            $"Are you sure you want to cancel your {item.ServiceType} appointment on {item.DateDisplay}?",
            "Yes, Cancel", "No");

        if (!confirm) return;

        var success = await Services.AppointmentService.Instance.CancelAsync(item.AppointmentId);

        if (success)
        {
            await RefreshAsync();
            await Shell.Current.DisplayAlert("Appointment Cancelled", "Your appointment has been cancelled.", "OK");
        }
    }

    [RelayCommand]
    private async Task AcceptSuggestedSchedule(AppointmentListItem? item)
    {
        if (item == null || !item.CanRespondToSuggestedSchedule)
            return;

        var accepted = await Services.AppointmentService.Instance
            .AcceptSuggestedFollowUpScheduleAsync(item.AppointmentId);
        if (!accepted)
        {
            await Shell.Current.DisplayAlert(
                "Schedule no longer available",
                "That suggested time is no longer available. Contact the clinic or submit a new request.",
                "OK");
            await RefreshAsync();
            return;
        }

        await RefreshAsync();
        await Shell.Current.DisplayAlert("Follow-Up Confirmed", "Your suggested schedule has been accepted.", "OK");
    }

    [RelayCommand]
    private async Task DeclineSuggestedSchedule(AppointmentListItem? item)
    {
        if (item == null || !item.CanRespondToSuggestedSchedule)
            return;

        var confirm = await Shell.Current.DisplayAlert(
            "Decline Suggested Schedule?",
            "The follow-up request will be cancelled. You can submit a new request later.",
            "Decline", "Keep Request");
        if (!confirm)
            return;

        var declined = await Services.AppointmentService.Instance
            .DeclineSuggestedFollowUpScheduleAsync(item.AppointmentId);
        if (!declined)
        {
            await Shell.Current.DisplayAlert("Request changed", "Refresh your appointments and try again.", "OK");
            await RefreshAsync();
            return;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RescheduleAppointment(AppointmentListItem? item)
    {
        if (item == null || !item.CanReschedule)
            return;

        await Shell.Current.Navigation.PushAsync(
            new Views.RescheduleAppointmentPage(item.AppointmentId));
    }

    [RelayCommand]
    private async Task GoBack()
    {
        if (IsDetailVisible)
        {
            IsDetailVisible = false;
            SelectedAppointment = null;
            OnPropertyChanged(nameof(HeaderTitle));
            return;
        }

        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoToHome() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task GoToNotifications()
        => await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());

    [RelayCommand]
    private async Task GoToProfile()
        => await Shell.Current.Navigation.PushAsync(new Views.ProfilePage());
}



