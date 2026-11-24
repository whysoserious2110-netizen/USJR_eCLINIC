using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.ViewModels;

public partial class FollowUpReviewViewModel : ObservableObject
{
    private readonly int _appointmentId;
    private Appointment? _appointment;
    private bool _isAdjustingTime;
    private TimeSpan _lastValidSelectedTime = Services.AppointmentShiftPolicy.MorningStart;
    private int _availabilityRequestVersion;

    [ObservableProperty] private string patientName = string.Empty;
    [ObservableProperty] private string clinicType = string.Empty;
    [ObservableProperty] private string requestedDate = string.Empty;
    [ObservableProperty] private string requestedTime = string.Empty;
    [ObservableProperty] private string requestReason = string.Empty;
    [ObservableProperty] private string followUpInstructions = string.Empty;
    [ObservableProperty] private string previousVisitDetails = string.Empty;
    [ObservableProperty] private string availabilityMessage = string.Empty;
    [ObservableProperty] private DateTime minimumDate = DateTime.Today;
    [ObservableProperty] private DateTime suggestedDate = DateTime.Today.AddDays(1);
    [ObservableProperty] private string selectedSession = $"{Services.AppointmentShiftPolicy.MorningShift} Shift";
    [ObservableProperty] private TimeSpan selectedTime = Services.AppointmentShiftPolicy.MorningStart;
    [ObservableProperty] private string reviewRemarks = string.Empty;
    [ObservableProperty] private bool isPageAvailable = true;
    [ObservableProperty] private bool hasAvailableTime;

    public IReadOnlyList<string> SessionOptions { get; } = new[]
    {
        $"{Services.AppointmentShiftPolicy.MorningShift} Shift",
        $"{Services.AppointmentShiftPolicy.AfternoonShift} Shift"
    };
    public string SelectedTimeDisplay => DateTime.Today.Add(SelectedTime).ToString("h:mm tt");
    public string SelectedSessionHint => SelectedSession == $"{Services.AppointmentShiftPolicy.MorningShift} Shift"
        ? "Morning starts: 7:00–11:00 AM · 1-hour visit"
        : "Afternoon starts: 1:00–6:00 PM · 1-hour visit";

    partial void OnHasAvailableTimeChanged(bool value) => OnPropertyChanged(nameof(CanSuggestSchedule));
    partial void OnSelectedSessionChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedSessionHint));
        SetSelectedTime(value == $"{Services.AppointmentShiftPolicy.AfternoonShift} Shift"
            ? Services.AppointmentShiftPolicy.AfternoonStart
            : Services.AppointmentShiftPolicy.MorningStart);
        _ = LoadAvailableTimeAsync();
    }

    partial void OnSelectedTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(SelectedTimeDisplay));
        if (_isAdjustingTime)
            return;

        var shift = Services.AppointmentShiftPolicy.GetShiftForAppointmentStart(value);
        var selectedShift = SelectedSession.StartsWith(Services.AppointmentShiftPolicy.MorningShift, StringComparison.Ordinal)
            ? Services.AppointmentShiftPolicy.MorningShift
            : Services.AppointmentShiftPolicy.AfternoonShift;
        if (shift == null || !string.Equals(shift, selectedShift, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _availabilityRequestVersion);
            _isAdjustingTime = true;
            SelectedTime = _lastValidSelectedTime;
            _isAdjustingTime = false;
            OnPropertyChanged(nameof(SelectedTimeDisplay));
            HasAvailableTime = false;
            AvailabilityMessage = GetTimeSelectionError(value, selectedShift);
            return;
        }

        _lastValidSelectedTime = value;
        _ = LoadAvailableTimeAsync();
    }

    public bool CanSuggestSchedule => HasAvailableTime;

    private void SetSelectedTime(TimeSpan time)
    {
        _isAdjustingTime = true;
        SelectedTime = time;
        _lastValidSelectedTime = time;
        _isAdjustingTime = false;
        OnPropertyChanged(nameof(SelectedTimeDisplay));
    }

    private static string GetTimeSelectionError(TimeSpan time, string shift)
    {
        if (time >= Services.AppointmentShiftPolicy.BreakStart && time < Services.AppointmentShiftPolicy.BreakEnd)
            return Services.AppointmentShiftPolicy.GetSlotPolicyError(time);

        return shift == Services.AppointmentShiftPolicy.MorningShift
            ? "Choose a Morning Shift start from 7:00 AM through 11:00 AM. The one-hour visit must end by noon."
            : "Choose an Afternoon Shift start from 1:00 PM through 6:00 PM. The one-hour visit must end by 7:00 PM.";
    }

    public FollowUpReviewViewModel(int appointmentId)
    {
        _appointmentId = appointmentId;
    }

    partial void OnSuggestedDateChanged(DateTime value)
    {
        _ = LoadAvailableTimeAsync();
    }

    public async Task InitializeAsync()
    {
        var reviewer = Services.AuthService.Instance.CurrentUser;
        _appointment = await Services.AppointmentService.Instance.GetByIdAsync(_appointmentId);
        var expectedRole = _appointment?.ServiceType == "Dental" ? "Dentist" : "Doctor";
        if (reviewer?.Role != expectedRole ||
            _appointment == null ||
            _appointment.Status != "Pending" ||
            !Services.AppointmentService.IsFollowUpAppointment(_appointment))
        {
            IsPageAvailable = false;
            return;
        }

        var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(_appointment.PatientEmail);
        PatientName = patient?.FullName ?? _appointment.PatientEmail;
        ClinicType = _appointment.ServiceType;
        RequestedDate = _appointment.VisitDate.ToString("dddd, MMMM d, yyyy");
        RequestedTime = _appointment.VisitTime;
        RequestReason = _appointment.ReasonOrPurpose;
        FollowUpInstructions = _appointment.FollowUpInstructions;

        var previousAppointment = _appointment.OriginalAppointmentId.HasValue
            ? await Services.AppointmentService.Instance.GetByIdAsync(_appointment.OriginalAppointmentId.Value)
            : null;

        if (previousAppointment != null && _appointment.ServiceType == "Medical")
        {
            var record = await Services.MedicalRecordService.Instance.GetByAppointmentIdAsync(previousAppointment.Id);
            PreviousVisitDetails = record == null
                ? $"Medical visit · {previousAppointment.VisitDate:MMM d, yyyy}"
                : $"Medical visit · {record.ConsultationDate:MMM d, yyyy}\n" +
                  $"Complaint: {record.ChiefComplaint}\nDiagnosis: {record.Diagnosis}\nTreatment: {record.Treatment}";
        }
        else if (previousAppointment != null)
        {
            var record = await Services.DentalRecordService.Instance.GetByAppointmentIdAsync(previousAppointment.Id);
            PreviousVisitDetails = record == null
                ? $"Dental visit · {previousAppointment.VisitDate:MMM d, yyyy}"
                : $"Dental visit · {record.VisitDate:MMM d, yyyy}\n" +
                  $"Findings: {record.ExamFindings}\nDiagnosis: {record.Diagnosis}\nTreatment: {record.TreatmentRendered}";
        }
        else
        {
            PreviousVisitDetails = "The original visit details are unavailable.";
        }

        MinimumDate = DateTime.Today;
        SuggestedDate = DateTime.Today.AddDays(1);
        if (SuggestedDate.DayOfWeek == DayOfWeek.Sunday)
            SuggestedDate = SuggestedDate.AddDays(1);
        await LoadAvailableTimeAsync();
    }

    private async Task LoadAvailableTimeAsync()
    {
        if (_appointment == null || !IsPageAvailable)
            return;

        var requestVersion = Interlocked.Increment(ref _availabilityRequestVersion);
        var date = SuggestedDate.Date;
        var selectedTime = SelectedTime;
        var timeText = DateTime.Today.Add(selectedTime).ToString("h:mm tt");
        HasAvailableTime = false;
        AvailabilityMessage = "Checking this time against clinic hours and staff schedules…";
        try
        {
            var error = await Services.AppointmentService.Instance.ValidateSlotAvailabilityAsync(
                date,
                _appointment.ServiceType,
                _appointment.SubService,
                timeText,
                _appointment.PatientEmail,
                _appointment.Id);

            if (requestVersion != Volatile.Read(ref _availabilityRequestVersion) ||
                SuggestedDate.Date != date || SelectedTime != selectedTime)
                return;

            HasAvailableTime = error == null;
            AvailabilityMessage = error ?? $"{timeText} is available for the suggested schedule.";
        }
        catch
        {
            if (requestVersion != Volatile.Read(ref _availabilityRequestVersion))
                return;

            HasAvailableTime = false;
            AvailabilityMessage = "The clinic schedule could not be loaded. Please try again.";
        }
    }

    [RelayCommand]
    private async Task Approve()
    {
        if (!await Shell.Current.DisplayAlert(
                "Approve Follow-Up?",
                $"Confirm {PatientName}'s {ClinicType.ToLowerInvariant()} follow-up for {RequestedDate} at {RequestedTime}?",
                "Approve", "Cancel"))
        {
            return;
        }

        var approved = await Services.AppointmentService.Instance.ApproveFollowUpAsync(_appointmentId);
        if (!approved)
        {
            await Shell.Current.DisplayAlert(
                "Schedule unavailable",
                "This clinician is no longer available for the requested time. Suggest another schedule or refresh the appointment list.",
                "OK");
            return;
        }

        await Shell.Current.DisplayAlert("Approved", "The patient has been notified.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task Decline()
    {
        var reason = await Shell.Current.DisplayPromptAsync(
            "Decline Follow-Up",
            "Enter the reason the patient will see:",
            "Decline", "Cancel", "Reason", maxLength: 240);
        if (string.IsNullOrWhiteSpace(reason))
            return;

        var declined = await Services.AppointmentService.Instance.DeclineFollowUpAsync(_appointmentId, reason);
        if (!declined)
        {
            await Shell.Current.DisplayAlert("Request changed", "Refresh the appointment list and review the latest status.", "OK");
            return;
        }

        await Shell.Current.DisplayAlert("Request declined", "The patient has been notified of the reason.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task SuggestSchedule()
    {
        if (_appointment == null || !HasAvailableTime)
        {
            await Shell.Current.DisplayAlert("Choose a schedule", "Select a date and available time first.", "OK");
            return;
        }

        var selectedTimeText = DateTime.Today.Add(SelectedTime).ToString("h:mm tt");
        var result = await Services.AppointmentService.Instance.SuggestFollowUpScheduleAsync(
            _appointmentId, SuggestedDate, selectedTimeText, ReviewRemarks);
        await Shell.Current.DisplayAlert(
            result.Success ? "Suggestion sent" : "Schedule unavailable",
            result.Message,
            "OK");
        if (result.Success)
            await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
