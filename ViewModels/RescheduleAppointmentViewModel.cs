using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using USJR_eCLINIC.Models;
using USJR_eCLINIC.Services;

namespace USJR_eCLINIC.ViewModels;

public partial class RescheduleAppointmentViewModel : ObservableObject
{
    private readonly int _appointmentId;
    private Appointment? _originalAppointment;
    private bool _isInitialized;
    private bool _isAdjustingTime;
    private TimeSpan _lastValidSelectedTime = AppointmentShiftPolicy.MorningStart;
    private int _availabilityRequestVersion;

    public RescheduleAppointmentViewModel(int appointmentId)
    {
        _appointmentId = appointmentId;
    }

    [ObservableProperty] private string originalSchedule = string.Empty;
    [ObservableProperty] private string reschedulingReason = string.Empty;
    [ObservableProperty] private string additionalReason = string.Empty;
    [ObservableProperty] private DateTime selectedDate = DateTime.Today;
    [ObservableProperty] private string selectedSession = $"{AppointmentShiftPolicy.MorningShift} Shift";
    [ObservableProperty] private TimeSpan selectedTime = AppointmentShiftPolicy.MorningStart;
    [ObservableProperty] private string availabilityMessage = string.Empty;
    [ObservableProperty] private bool hasAvailableSlots;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string loadError = string.Empty;

    public DateTime MinimumDate => DateTime.Today;
    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadError);
    public bool CanSubmit => _originalAppointment != null && HasAvailableSlots && !IsSaving;
    public string SelectedTimeDisplay => DateTime.Today.Add(SelectedTime).ToString("h:mm tt");
    public string SelectedSessionHint => SelectedSession == $"{AppointmentShiftPolicy.MorningShift} Shift"
        ? "Morning starts: 7:00–11:00 AM · 1-hour visit"
        : "Afternoon starts: 1:00–6:00 PM · 1-hour visit";

    public IReadOnlyList<string> SessionOptions { get; } = new[]
    {
        $"{AppointmentShiftPolicy.MorningShift} Shift",
        $"{AppointmentShiftPolicy.AfternoonShift} Shift"
    };

    partial void OnLoadErrorChanged(string value) => OnPropertyChanged(nameof(HasLoadError));
    partial void OnHasAvailableSlotsChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));
    partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(CanSubmit));

    partial void OnSelectedDateChanged(DateTime value)
    {
        _ = RefreshAvailableSlotsAsync();
    }

    partial void OnSelectedSessionChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedSessionHint));
        var start = value == $"{AppointmentShiftPolicy.AfternoonShift} Shift"
            ? AppointmentShiftPolicy.AfternoonStart
            : AppointmentShiftPolicy.MorningStart;
        SetSelectedTime(start);
        _ = RefreshAvailableSlotsAsync();
    }

    partial void OnSelectedTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(SelectedTimeDisplay));
        if (_isAdjustingTime)
            return;

        var shift = AppointmentShiftPolicy.GetShiftForAppointmentStart(value);
        var selectedShift = SelectedSession.StartsWith(AppointmentShiftPolicy.MorningShift, StringComparison.Ordinal)
            ? AppointmentShiftPolicy.MorningShift
            : AppointmentShiftPolicy.AfternoonShift;
        if (shift == null || !string.Equals(shift, selectedShift, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _availabilityRequestVersion);
            _isAdjustingTime = true;
            SelectedTime = _lastValidSelectedTime;
            _isAdjustingTime = false;
            OnPropertyChanged(nameof(SelectedTimeDisplay));
            HasAvailableSlots = false;
            AvailabilityMessage = GetTimeSelectionError(value, selectedShift);
            return;
        }

        _lastValidSelectedTime = value;
        _ = RefreshAvailableSlotsAsync();
    }

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
        if (time >= AppointmentShiftPolicy.BreakStart && time < AppointmentShiftPolicy.BreakEnd)
            return AppointmentShiftPolicy.GetSlotPolicyError(time);

        return shift == AppointmentShiftPolicy.MorningShift
            ? "Choose a Morning Shift start from 7:00 AM through 11:00 AM. The one-hour visit must end by noon."
            : "Choose an Afternoon Shift start from 1:00 PM through 6:00 PM. The one-hour visit must end by 7:00 PM.";
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        var patient = AuthService.Instance.CurrentUser;
        if (patient == null || patient.Role is not ("Student" or "Faculty" or "Admin Personnel" or "Non-Teaching"))
        {
            LoadError = "Sign in with a patient account to request a new schedule.";
            return;
        }

        _originalAppointment = await AppointmentService.Instance.GetByIdAsync(_appointmentId);
        if (_originalAppointment == null ||
            !_originalAppointment.PatientEmail.Equals(patient.Email, StringComparison.OrdinalIgnoreCase))
        {
            LoadError = "This appointment could not be found for the signed-in patient.";
            return;
        }

        if (_originalAppointment.Status is not ("Reschedule Required" or "Missed" or "Declined" or "Expired"))
        {
            LoadError = "This appointment is not waiting for a new schedule.";
            return;
        }

        var shift = string.IsNullOrWhiteSpace(_originalAppointment.AssignedShift)
            ? "Shift not recorded"
            : $"{_originalAppointment.AssignedShift} Shift";
        OriginalSchedule = $"{_originalAppointment.VisitDate:MMM d, yyyy} at {_originalAppointment.VisitTime} · {shift}";
        ReschedulingReason = _originalAppointment.ReschedulingReason;
        await RefreshAvailableSlotsAsync();
    }

    public async Task RefreshAvailableSlotsAsync()
    {
        if (_originalAppointment == null)
            return;

        var patient = AuthService.Instance.CurrentUser;
        if (patient == null)
            return;

        var requestVersion = Interlocked.Increment(ref _availabilityRequestVersion);
        var date = SelectedDate.Date;
        var time = SelectedTime;
        var timeText = DateTime.Today.Add(time).ToString("h:mm tt");
        HasAvailableSlots = false;
        AvailabilityMessage = "Checking this time against clinic hours and staff schedules…";

        try
        {
            var error = await AppointmentService.Instance.ValidateSlotAvailabilityAsync(
                date,
                _originalAppointment.ServiceType,
                _originalAppointment.SubService,
                timeText,
                patient.Email,
                _originalAppointment.Id);

            if (requestVersion != Volatile.Read(ref _availabilityRequestVersion) ||
                SelectedDate.Date != date || SelectedTime != time)
                return;

            HasAvailableSlots = error == null;
            AvailabilityMessage = error ?? $"{timeText} is available. Your request still needs clinic approval.";
        }
        catch
        {
            if (requestVersion != Volatile.Read(ref _availabilityRequestVersion))
                return;

            HasAvailableSlots = false;
            AvailabilityMessage = "The clinic schedule could not be checked. Please try again.";
        }
    }

    [RelayCommand]
    private async Task SubmitReschedule()
    {
        if (_originalAppointment == null || !HasAvailableSlots || IsSaving)
            return;

        IsSaving = true;
        try
        {
            var selectedTimeText = DateTime.Today.Add(SelectedTime).ToString("h:mm tt");
            var result = await AppointmentService.Instance.RequestRescheduleAsync(
                _originalAppointment.Id,
                SelectedDate,
                selectedTimeText,
                AdditionalReason);
            await Shell.Current.DisplayAlert(
                result.Success ? "Request Submitted" : "Unable to Reschedule",
                result.Message,
                "OK");

            if (result.Success)
                await Shell.Current.Navigation.PopAsync();
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Unable to Reschedule",
                "The new schedule request could not be saved. Please try again.",
                "OK");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
