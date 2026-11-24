using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using System.Globalization;
using USJR_eCLINIC.Services;
namespace USJR_eCLINIC.ViewModels;

public sealed class FollowUpRecommendationOption
{
    public int OriginalAppointmentId { get; init; }
    public string ClinicType { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public DateTime VisitDate { get; init; }
    public DateTime? RecommendedFollowUpDate { get; init; }
    public string Instructions { get; init; } = string.Empty;
    public string DisplayText => $"{ClinicType} visit · {VisitDate:MMM d, yyyy} · {ProviderName}";
    public string RecommendationDetails =>
        $"{(string.IsNullOrWhiteSpace(Instructions) ? "Follow-up recommended by your clinician." : Instructions)}" +
        (RecommendedFollowUpDate.HasValue
            ? $" Suggested date: {RecommendedFollowUpDate.Value:MMM d, yyyy}."
            : string.Empty);
}

public partial class BookAppointmentViewModel : ObservableObject
{
    public ObservableCollection<string> ServiceTypes { get; } = new()
    {
        "Medical", "Dental", "Follow-up", "Cert Request"
    };

    [ObservableProperty]
    private string selectedServiceType = "Medical";

    public bool IsMedical => SelectedServiceType == "Medical";
    public bool IsDental => SelectedServiceType == "Dental";
    public bool IsFollowUp => SelectedServiceType == "Follow-up";
    public bool IsCertRequest => SelectedServiceType == "Cert Request";

    private bool _isResettingSelections;
    private bool _isUpdatingSelectedTime;
    private string? _unavailableDateMessage;
    private int _dateRequestVersion;
    private int _timeAvailabilityRequestVersion;
    private TimeSpan _lastValidSelectedTime = AppointmentShiftPolicy.MorningStart;
    private bool _isTimeSelectionValid = true;

    public ObservableCollection<string> MedicalReasons { get; } = new()
    {
        "General Consultation", "Fever / Temperature", "Headache", "Cough / Cold",
        "Stomach Pain", "Injury / Minor Accident", "Medication Concern",
        "Follow-Up Consultation", "Health Concern", "Other"
    };

    [ObservableProperty]
    private string selectedMedicalReason = string.Empty;

    public bool ShowOtherReason => SelectedMedicalReason == "Other";

    [ObservableProperty]
    private string otherReasonText = string.Empty;

    public ObservableCollection<string> DentalServices { get; } = new()
    {
        "Oral Prophylaxis", "Tooth Restoration", "Tooth Extraction", "Dental Consultation"
    };

    [ObservableProperty]
    private string selectedDentalService = string.Empty;

    public bool IsDentalConsultation => SelectedDentalService == "Dental Consultation";

    public bool IsDentalWithSchedule =>
        IsDental &&
        !string.IsNullOrWhiteSpace(SelectedDentalService) &&
        !IsDentalConsultation;

    public ObservableCollection<string> FollowUpReasons { get; } = new()
    {
        "Follow-up consultation", "Re-evaluation", "Treatment follow-up",
        "Medication follow-up", "Other"
    };

    [ObservableProperty]
    private string selectedFollowUpClinicType = string.Empty;

    [ObservableProperty]
    private string selectedFollowUpReason = string.Empty;

    [ObservableProperty]
    private FollowUpRecommendationOption? selectedFollowUpRecommendation;

    [ObservableProperty]
    private bool hasFollowUpRecommendations;

    public ObservableCollection<FollowUpRecommendationOption> FollowUpRecommendations { get; } = new();

    public bool HasSelectedFollowUpRecommendation => SelectedFollowUpRecommendation != null;
    public string SelectedFollowUpRecommendationDetails =>
        SelectedFollowUpRecommendation?.RecommendationDetails ?? string.Empty;

    [ObservableProperty]
    private string additionalNotes = string.Empty;

    public ObservableCollection<string> CertificateTypes { get; } = new()
    {
        "Medical Certificate", "Dental Certificate"
    };

    [ObservableProperty]
    private string selectedCertificateType = string.Empty;

    [ObservableProperty]
    private string certificatePurpose = string.Empty;

    [ObservableProperty]
    private DateTime? selectedDate;

    public string DateDisplayText =>
        SelectedDate.HasValue
            ? SelectedDate.Value.ToString("MMM dd, yyyy (ddd)")
            : "Select a date";

    public bool HasSelectedDate => SelectedDate.HasValue;

    public ObservableCollection<string> SessionOptions { get; } = new()
    {
        $"{AppointmentShiftPolicy.MorningShift} Shift",
        $"{AppointmentShiftPolicy.AfternoonShift} Shift"
    };
    public ObservableCollection<CalendarDayCell> CalendarDays { get; } = new();
    [ObservableProperty] private bool isCalendarVisible;
    [ObservableProperty] private DateTime displayedCalendarMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public string CalendarMonthTitle => DisplayedCalendarMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    public string SelectedTimeDisplay => DateTime.Today.Add(SelectedTime).ToString("h:mm tt", CultureInfo.CurrentCulture);
    public string SelectedSessionHint => SelectedSession == $"{AppointmentShiftPolicy.MorningShift} Shift"
        ? "Morning appointments start from 7:00 AM through 11:00 AM and last one hour."
        : "Afternoon appointments start from 1:00 PM through 6:00 PM and last one hour.";

    [ObservableProperty] private string selectedSession = $"{AppointmentShiftPolicy.MorningShift} Shift";
    [ObservableProperty] private TimeSpan selectedTime = AppointmentShiftPolicy.MorningStart;

    [ObservableProperty] private bool hasNoAvailableTimeOptions;
    [ObservableProperty] private string availabilityMessage = string.Empty;

    [ObservableProperty]
    private string? selectedTimeOption;

    public bool ShowDateTimeSection =>
        IsMedical ||
        (IsDental && IsDentalWithSchedule) ||
        IsFollowUp ||
        IsCertRequest;

    public BookAppointmentViewModel()
    {
    }

    partial void OnSelectedServiceTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsMedical));
        OnPropertyChanged(nameof(IsDental));
        OnPropertyChanged(nameof(IsFollowUp));
        OnPropertyChanged(nameof(IsCertRequest));
        OnPropertyChanged(nameof(ShowDateTimeSection));
        ResetSelections();
    }

    partial void OnSelectedMedicalReasonChanged(string value)
    {
        OnPropertyChanged(nameof(ShowOtherReason));
        ResetDateAfterSubServiceChange();
    }

    partial void OnSelectedDentalServiceChanged(string value)
    {
        OnPropertyChanged(nameof(IsDentalConsultation));
        OnPropertyChanged(nameof(IsDentalWithSchedule));
        OnPropertyChanged(nameof(ShowDateTimeSection));
        ResetDateAfterSubServiceChange();
    }

    partial void OnSelectedFollowUpClinicTypeChanged(string value)
    {
        ResetDateAfterSubServiceChange();
    }

    partial void OnSelectedFollowUpRecommendationChanged(FollowUpRecommendationOption? value)
    {
        OnPropertyChanged(nameof(HasSelectedFollowUpRecommendation));
        OnPropertyChanged(nameof(SelectedFollowUpRecommendationDetails));
        if (value == null)
            return;

        _isResettingSelections = true;
        SelectedFollowUpClinicType = value.ClinicType;
        _isResettingSelections = false;
        ResetDateAfterSubServiceChange();
    }

    partial void OnSelectedFollowUpReasonChanged(string value)
    {
        ResetDateAfterSubServiceChange();
    }

    partial void OnSelectedCertificateTypeChanged(string value)
    {
        ResetDateAfterSubServiceChange();
    }

    partial void OnDisplayedCalendarMonthChanged(DateTime value)
    {
        OnPropertyChanged(nameof(CalendarMonthTitle));
        RefreshCalendarDays();
    }

    partial void OnSelectedSessionChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedSessionHint));
        var time = value == $"{AppointmentShiftPolicy.AfternoonShift} Shift"
            ? AppointmentShiftPolicy.AfternoonStart
            : AppointmentShiftPolicy.MorningStart;
        SetSelectedTime(time);
        _ = RefreshSelectedTimeAvailabilityAsync();
    }

    partial void OnSelectedTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(SelectedTimeDisplay));
        if (_isUpdatingSelectedTime)
            return;

        var expectedShift = AppointmentShiftPolicy.GetShiftForAppointmentStart(value);
        var selectedShift = SelectedSession.StartsWith(AppointmentShiftPolicy.MorningShift, StringComparison.Ordinal)
            ? AppointmentShiftPolicy.MorningShift
            : AppointmentShiftPolicy.AfternoonShift;
        if (expectedShift == null || !string.Equals(expectedShift, selectedShift, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _timeAvailabilityRequestVersion);
            _isTimeSelectionValid = false;
            var invalidMessage = GetTimeSelectionError(value, selectedShift);
            _isUpdatingSelectedTime = true;
            SelectedTime = _lastValidSelectedTime;
            _isUpdatingSelectedTime = false;
            OnPropertyChanged(nameof(SelectedTimeDisplay));
            AvailabilityMessage = invalidMessage;
            HasNoAvailableTimeOptions = true;
            return;
        }

        _isTimeSelectionValid = true;
        _lastValidSelectedTime = value;
        SelectedTimeOption = DateTime.Today.Add(value).ToString("h:mm tt", CultureInfo.CurrentCulture);
        _ = RefreshSelectedTimeAvailabilityAsync();
    }

    private void SetSelectedTime(TimeSpan time)
    {
        _isUpdatingSelectedTime = true;
        SelectedTime = time;
        _lastValidSelectedTime = time;
        _isTimeSelectionValid = true;
        SelectedTimeOption = DateTime.Today.Add(time).ToString("h:mm tt", CultureInfo.CurrentCulture);
        _isUpdatingSelectedTime = false;
        OnPropertyChanged(nameof(SelectedTimeDisplay));
    }

    private static string GetTimeSelectionError(TimeSpan time, string shift)
    {
        var policyError = AppointmentShiftPolicy.GetSlotPolicyError(time);
        if (time >= AppointmentShiftPolicy.BreakStart && time < AppointmentShiftPolicy.BreakEnd)
            return policyError;

        return shift == AppointmentShiftPolicy.MorningShift
            ? "Choose a Morning Shift start from 7:00 AM through 11:00 AM. The one-hour visit must end by noon."
            : "Choose an Afternoon Shift start from 1:00 PM through 6:00 PM. The one-hour visit must end by 7:00 PM.";
    }

    private void ResetDateAfterSubServiceChange()
    {
        if (_isResettingSelections)
            return;

        ClearDateSelection(closeCalendar: true);
    }

    private void ClearDateSelection(bool closeCalendar)
    {
        Interlocked.Increment(ref _dateRequestVersion);
        Interlocked.Increment(ref _timeAvailabilityRequestVersion);
        _unavailableDateMessage = null;
        SelectedDate = null;
        _isTimeSelectionValid = true;
        SelectedTimeOption = DateTime.Today.Add(SelectedTime).ToString("h:mm tt", CultureInfo.CurrentCulture);
        HasNoAvailableTimeOptions = false;
        AvailabilityMessage = string.Empty;
        OnPropertyChanged(nameof(HasSelectedDate));
        OnPropertyChanged(nameof(DateDisplayText));

        if (closeCalendar) IsCalendarVisible = false;
    }

    private async Task HandleDatePickedAsync(DateTime date, int requestVersion)
    {
        if (!ShowDateTimeSection)
            return;

        var subService = GetCurrentSubService();
        if (string.IsNullOrWhiteSpace(subService))
        {
            if (requestVersion == Volatile.Read(ref _dateRequestVersion))
            {
                _unavailableDateMessage = "Please choose a service before selecting a date.";
                ClearInvalidDateSelection();
            }

            return;
        }

        try
        {
            var schedule = Services.ClinicScheduleService.Instance;
            var unavailableReason = await schedule.GetDateUnavailableReasonAsync(
                date,
                GetEffectiveServiceType(),
                subService);

            if (requestVersion != Volatile.Read(ref _dateRequestVersion))
                return;

            if (unavailableReason != null)
            {
                _unavailableDateMessage = unavailableReason;
                ClearInvalidDateSelection();
                await Shell.Current.DisplayAlert("Date Not Available", unavailableReason, "OK");
                return;
            }

            _unavailableDateMessage = null;
            SelectedDate = date.Date;
            _isTimeSelectionValid = true;
            HasNoAvailableTimeOptions = false;
            AvailabilityMessage = string.Empty;
            OnPropertyChanged(nameof(HasSelectedDate));
            OnPropertyChanged(nameof(DateDisplayText));

            SelectedTimeOption = DateTime.Today.Add(SelectedTime).ToString("h:mm tt", CultureInfo.CurrentCulture);
            await RefreshSelectedTimeAvailabilityAsync(requestVersion);
        }
        catch
        {
            if (requestVersion != Volatile.Read(ref _dateRequestVersion))
                return;

            _unavailableDateMessage = "The clinic schedule could not be loaded. Please try again.";
            ClearInvalidDateSelection();
            await Shell.Current.DisplayAlert("Schedule Unavailable", _unavailableDateMessage, "OK");
        }
    }

    private void ClearInvalidDateSelection()
    {
        SelectedDate = null;
        _isTimeSelectionValid = true;
        SelectedTimeOption = DateTime.Today.Add(SelectedTime).ToString("h:mm tt", CultureInfo.CurrentCulture);
        HasNoAvailableTimeOptions = false;
        AvailabilityMessage = string.Empty;
        OnPropertyChanged(nameof(HasSelectedDate));
        OnPropertyChanged(nameof(DateDisplayText));

        IsCalendarVisible = false;
    }

    private void ResetSelections()
    {
        _isResettingSelections = true;
        SelectedDentalService = string.Empty;
        SelectedMedicalReason = string.Empty;
        SelectedFollowUpClinicType = string.Empty;
        SelectedFollowUpReason = string.Empty;
        SelectedFollowUpRecommendation = null;
        SelectedCertificateType = string.Empty;
        OtherReasonText = string.Empty;
        AdditionalNotes = string.Empty;
        CertificatePurpose = string.Empty;
        _isResettingSelections = false;
        ClearDateSelection(closeCalendar: false);
        IsCalendarVisible = false;
        ClearDateSelection(closeCalendar: false);
        IsCalendarVisible = false;
    }

    public async Task LoadFollowUpRecommendationsAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null)
            return;

        FollowUpRecommendations.Clear();
        var appointments = Services.AppointmentService.Instance;
        var patientAppointments = await appointments.GetAllForPatientAsync(user.Email);

        bool HasActiveRequestFor(int originalAppointmentId) =>
            patientAppointments.Any(appointment =>
                appointment.OriginalAppointmentId == originalAppointmentId &&
                appointment.Status is "Pending" or "Confirmed" or "Reschedule Requested" or "CheckedIn" or "VitalsRecorded");

        var medicalRecords = await Services.MedicalRecordService.Instance.GetForPatientAsync(user.Email);
        foreach (var record in medicalRecords.Where(record => record.FollowUpRecommended && record.AppointmentId > 0))
        {
            var previousVisit = await appointments.GetByIdAsync(record.AppointmentId);
            if (previousVisit == null || previousVisit.Status != "Completed" ||
                Services.AppointmentService.GetClinicalType(previousVisit) != "Medical" ||
                HasActiveRequestFor(previousVisit.Id))
                continue;

            FollowUpRecommendations.Add(new FollowUpRecommendationOption
            {
                OriginalAppointmentId = previousVisit.Id,
                ClinicType = "Medical",
                ProviderName = string.IsNullOrWhiteSpace(record.AttendingStaff) ? "Doctor" : record.AttendingStaff,
                VisitDate = record.ConsultationDate,
                RecommendedFollowUpDate = record.RecommendedFollowUpDate,
                Instructions = record.FollowUpNotes
            });
        }

        var dentalRecords = await Services.DentalRecordService.Instance.GetForPatientAsync(user.Email);
        foreach (var record in dentalRecords.Where(record => record.FollowUpRecommended && record.AppointmentId > 0))
        {
            var previousVisit = await appointments.GetByIdAsync(record.AppointmentId);
            if (previousVisit == null || previousVisit.Status != "Completed" ||
                Services.AppointmentService.GetClinicalType(previousVisit) != "Dental" ||
                HasActiveRequestFor(previousVisit.Id))
                continue;

            FollowUpRecommendations.Add(new FollowUpRecommendationOption
            {
                OriginalAppointmentId = previousVisit.Id,
                ClinicType = "Dental",
                ProviderName = string.IsNullOrWhiteSpace(record.AttendingDentist) ? "Dentist" : record.AttendingDentist,
                VisitDate = record.VisitDate,
                RecommendedFollowUpDate = record.RecommendedFollowUpDate,
                Instructions = record.CareInstructions
            });
        }

        HasFollowUpRecommendations = FollowUpRecommendations.Count > 0;
    }

    private string GetEffectiveServiceType() =>
        IsFollowUp ? SelectedFollowUpClinicType : SelectedServiceType;

    private string GetCurrentSubService()
    {
        if (IsMedical)
            return SelectedMedicalReason;
        if (IsDental)
            return SelectedDentalService;
        if (IsFollowUp)
            return string.IsNullOrWhiteSpace(SelectedFollowUpClinicType)
                ? string.Empty
                : $"{SelectedFollowUpClinicType} Follow-up";
        return SelectedCertificateType;
    }

    [RelayCommand]
    private void SelectServiceType(string type) => SelectedServiceType = type;

    [RelayCommand]
    private async Task GoToAppointments()
    {
        await Shell.Current.Navigation.PushAsync(new Views.AppointmentsPage());
    }

    [RelayCommand]
    private async Task GoToNotifications()
    {
        await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());
    }

    [RelayCommand]
    private async Task GoToProfile()
    {
        await Shell.Current.Navigation.PushAsync(new Views.ProfilePage());
    }

    [RelayCommand]
    private void SelectDentalService(string service) => SelectedDentalService = service;

    [RelayCommand]
    private void SelectMedicalReason(string reason) => SelectedMedicalReason = reason;

    [RelayCommand]
    private void SelectFollowUpReason(string reason) => SelectedFollowUpReason = reason;

    [RelayCommand]
    private void SelectCertificateType(string type) => SelectedCertificateType = type;

    [RelayCommand]
    private async Task OpenCalendar()
    {
        var subService = GetCurrentSubService();
        if (string.IsNullOrWhiteSpace(subService))
        {
            await Shell.Current.DisplayAlert("Choose a service", "Select the service details before choosing an appointment date.", "OK");
            return;
        }

        DisplayedCalendarMonth = new DateTime(
            (SelectedDate ?? DateTime.Today).Year,
            (SelectedDate ?? DateTime.Today).Month,
            1);
        RefreshCalendarDays();
        IsCalendarVisible = true;
    }

    [RelayCommand]
    private void CloseCalendar() => IsCalendarVisible = false;

    [RelayCommand]
    private void ChangeCalendarMonth(int offset)
    {
        var nextMonth = DisplayedCalendarMonth.AddMonths(offset);
        if (nextMonth.Year < DateTime.Today.Year ||
            (nextMonth.Year == DateTime.Today.Year && nextMonth.Month < DateTime.Today.Month))
            return;

        DisplayedCalendarMonth = nextMonth;
    }

    [RelayCommand]
    private void PreviousCalendarMonth() => ChangeCalendarMonth(-1);

    [RelayCommand]
    private void NextCalendarMonth() => ChangeCalendarMonth(1);

    [RelayCommand]
    private async Task SelectCalendarDate(CalendarDayCell? cell)
    {
        if (cell == null || !cell.IsSelectable || cell.Date.DayOfWeek == DayOfWeek.Sunday)
            return;

        IsCalendarVisible = false;
        var version = Interlocked.Increment(ref _dateRequestVersion);
        await HandleDatePickedAsync(cell.Date, version);
        RefreshCalendarDays();
    }

    private void RefreshCalendarDays()
    {
        CalendarDays.Clear();
        var firstOfMonth = new DateTime(DisplayedCalendarMonth.Year, DisplayedCalendarMonth.Month, 1);
        var gridStart = firstOfMonth.AddDays(-(int)firstOfMonth.DayOfWeek);

        for (var index = 0; index < 42; index++)
        {
            var date = gridStart.AddDays(index);
            var inMonth = date.Month == DisplayedCalendarMonth.Month;
            var selectable = inMonth && date.Date >= DateTime.Today && date.DayOfWeek != DayOfWeek.Sunday;
            var selected = SelectedDate?.Date == date.Date;
            var background = selected ? Color.FromArgb("#F4C400") : Colors.White;
            var text = selectable ? Color.FromArgb("#17221B") : Color.FromArgb("#AAB2B8");
            if (!inMonth) text = Color.FromArgb("#D0D5D9");
            if (date.DayOfWeek == DayOfWeek.Sunday) text = Color.FromArgb("#AAB2B8");

            CalendarDays.Add(new CalendarDayCell
            {
                Date = date.Date,
                DayNumber = date.Day.ToString(CultureInfo.CurrentCulture),
                IsCurrentMonth = inMonth,
                IsAvailable = selectable,
                IsSelectable = selectable,
                IsToday = date.Date == DateTime.Today,
                IsSelected = selected,
                TextColor = text,
                CellBackgroundColor = inMonth ? background : Color.FromArgb("#F4F5F6"),
                CellBorderColor = selected ? Color.FromArgb("#087A3D") :
                    date.Date == DateTime.Today ? Color.FromArgb("#087A3D") : Colors.Transparent
            });
        }
    }

    private async Task RefreshSelectedTimeAvailabilityAsync(int dateRequestVersion = -1)
    {
        if (!SelectedDate.HasValue)
            return;

        var subService = GetCurrentSubService();
        if (string.IsNullOrWhiteSpace(subService))
        {
            HasNoAvailableTimeOptions = false;
            AvailabilityMessage = string.Empty;
            return;
        }

        var requestVersion = Interlocked.Increment(ref _timeAvailabilityRequestVersion);
        var expectedDateRequest = dateRequestVersion < 0
            ? Volatile.Read(ref _dateRequestVersion)
            : dateRequestVersion;
        var date = SelectedDate.Value.Date;
        var serviceType = GetEffectiveServiceType();
        var selectedTime = SelectedTime;
        var timeText = DateTime.Today.Add(selectedTime).ToString("h:mm tt", CultureInfo.CurrentCulture);
        SelectedTimeOption = timeText;
        HasNoAvailableTimeOptions = false;
        AvailabilityMessage = "Checking this time against clinic hours and staff schedules…";

        try
        {
            var patientEmail = Services.AuthService.Instance.CurrentUser?.Email;
            var error = await Services.AppointmentService.Instance.ValidateSlotAvailabilityAsync(
                date, serviceType, subService, timeText, patientEmail);

            if (requestVersion != Volatile.Read(ref _timeAvailabilityRequestVersion) ||
                expectedDateRequest != Volatile.Read(ref _dateRequestVersion) ||
                SelectedDate?.Date != date ||
                !string.Equals(subService, GetCurrentSubService(), StringComparison.Ordinal) ||
                SelectedTime != selectedTime)
            {
                return;
            }

            HasNoAvailableTimeOptions = error != null;
            AvailabilityMessage = error ?? $"{timeText} is available. The appointment lasts one hour.";
        }
        catch
        {
            if (requestVersion != Volatile.Read(ref _timeAvailabilityRequestVersion))
                return;

            HasNoAvailableTimeOptions = true;
            AvailabilityMessage = "Availability could not be checked. Please choose another time or try again.";
        }
    }

    [RelayCommand]
    private async Task Confirm()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null)
        {
            await Shell.Current.DisplayAlert("Not logged in", "Please log in again.", "OK");
            return;
        }

        string subService;
        string reasonOrPurpose = string.Empty;

        if (IsMedical)
        {
            if (string.IsNullOrWhiteSpace(SelectedMedicalReason))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please select a reason for visit.", "OK");
                return;
            }

            subService = SelectedMedicalReason;
            if (ShowOtherReason)
                reasonOrPurpose = OtherReasonText;
        }
        else if (IsDental)
        {
            if (string.IsNullOrWhiteSpace(SelectedDentalService))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please select a dental service.", "OK");
                return;
            }

            subService = SelectedDentalService;
            if (IsDentalConsultation)
            {
                await Shell.Current.DisplayAlert(
                    "Dental Consultation",
                    "This is a walk-in service. No appointment is required — please visit the clinic during operating hours.",
                    "OK");
                return;
            }
        }
        else if (IsFollowUp)
        {
            if (SelectedFollowUpRecommendation == null ||
                SelectedFollowUpClinicType is not ("Medical" or "Dental"))
            {
                await Shell.Current.DisplayAlert(
                    "Previous visit required",
                    "Choose a completed visit with a Doctor or Dentist follow-up recommendation.",
                    "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedFollowUpReason))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please select a follow-up reason.", "OK");
                return;
            }

            subService = $"{SelectedFollowUpClinicType} Follow-up";
            reasonOrPurpose = string.IsNullOrWhiteSpace(AdditionalNotes)
                ? SelectedFollowUpReason
                : $"{SelectedFollowUpReason}. {AdditionalNotes.Trim()}";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(SelectedCertificateType))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please select Medical Certificate or Dental Certificate.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(CertificatePurpose))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please enter the purpose for the paper certificate request.", "OK");
                return;
            }

            subService = SelectedCertificateType;
            reasonOrPurpose = CertificatePurpose.Trim();
        }

        if (ShowDateTimeSection)
        {
            if (_unavailableDateMessage != null)
            {
                await Shell.Current.DisplayAlert(
                    "Date Not Available",
                    _unavailableDateMessage,
                    "OK");
                return;
            }

            if (!SelectedDate.HasValue || string.IsNullOrWhiteSpace(SelectedTimeOption) || !_isTimeSelectionValid)
            {
                await Shell.Current.DisplayAlert("Missing or invalid time", "Please select a date and a valid time within the selected clinic shift.", "OK");
                return;
            }

            string? scheduleError;
            try
            {
                scheduleError = await Services.AppointmentService.Instance.ValidateSlotAvailabilityAsync(
                    SelectedDate.Value,
                    GetEffectiveServiceType(),
                    subService,
                    SelectedTimeOption,
                    user.Email);
            }
            catch
            {
                await Shell.Current.DisplayAlert(
                    "Schedule Unavailable",
                    "The clinic schedule could not be loaded. Please try again.",
                    "OK");
                return;
            }

            if (scheduleError != null)
            {
                await Shell.Current.DisplayAlert("Time Not Available", scheduleError, "OK");
                return;
            }
        }

        var confirmationMessage = ShowDateTimeSection && SelectedDate.HasValue
            ? $"Request {subService} for {SelectedDate.Value:MMM d, yyyy} at {SelectedTimeOption} ({SelectedSession})? Each appointment lasts one hour."
            : "Review your appointment details before submitting your request.";
        bool proceed = await Shell.Current.DisplayAlert(
            "Confirm Appointment Request?",
            confirmationMessage,
            "Confirm",
            "Cancel");

        if (!proceed)
            return;

        var appointment = new Models.Appointment
        {
            PatientEmail = user.Email,
            ServiceType = GetEffectiveServiceType(),
            SubService = subService,
            ReasonOrPurpose = reasonOrPurpose,
            OriginalAppointmentId = IsFollowUp ? SelectedFollowUpRecommendation?.OriginalAppointmentId : null,
            RecommendedByStaffId = IsFollowUp
                ? SelectedFollowUpRecommendation?.ClinicType == "Medical"
                    ? (await Services.MedicalRecordService.Instance.GetByAppointmentIdAsync(SelectedFollowUpRecommendation.OriginalAppointmentId))?.AttendingStaffId
                    : (await Services.DentalRecordService.Instance.GetByAppointmentIdAsync(SelectedFollowUpRecommendation!.OriginalAppointmentId))?.AttendingDentistId
                : null,
            RecommendedFollowUpDate = IsFollowUp ? SelectedFollowUpRecommendation?.RecommendedFollowUpDate : null,
            FollowUpInstructions = IsFollowUp ? SelectedFollowUpRecommendation?.Instructions ?? string.Empty : string.Empty,
            VisitDate = ShowDateTimeSection ? SelectedDate!.Value.Date : DateTime.Today,
            VisitTime = ShowDateTimeSection ? SelectedTimeOption! : string.Empty,
            Status = "Pending",
            CertificateStatus = IsCertRequest ? "Awaiting Appointment Approval" : string.Empty
        };

        try
        {
            var result = await Services.AppointmentService.Instance.BookAsync(appointment);
            if (!result.Success)
            {
                await Shell.Current.DisplayAlert("Appointment Not Available", result.Message, "OK");
                return;
            }

            await Shell.Current.DisplayAlert("Appointment Request", result.Message, "OK");
            await Shell.Current.Navigation.PopAsync();
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Unable to Submit",
                "The request could not be saved. Please try again.",
                "OK");
        }
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}


