using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

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

    private bool _isResettingPickerDate;
    private bool _isResettingSelections;
    private string? _unavailableDateMessage;
    private int _dateRequestVersion;

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
    private string selectedFollowUpReason = string.Empty;

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
    private DateTime pickerDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private DateTime? selectedDate;

    public string DateDisplayText =>
        SelectedDate.HasValue
            ? SelectedDate.Value.ToString("MMM dd, yyyy (ddd)")
            : "Select a date";

    public bool HasSelectedDate => SelectedDate.HasValue;

    public ObservableCollection<string> AvailableTimeOptions { get; } = new();

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

    partial void OnSelectedFollowUpReasonChanged(string value)
    {
        ResetDateAfterSubServiceChange();
    }

    partial void OnSelectedCertificateTypeChanged(string value)
    {
        ResetDateAfterSubServiceChange();
    }

    partial void OnPickerDateChanged(DateTime value)
    {
        if (_isResettingPickerDate)
            return;

        var version = Interlocked.Increment(ref _dateRequestVersion);
        _ = HandleDatePickedAsync(value, version);
    }

    private void ResetDateAfterSubServiceChange()
    {
        if (_isResettingSelections)
            return;

        ClearDateSelection(resetPickerToToday: true);
    }

    private void ClearDateSelection(bool resetPickerToToday)
    {
        Interlocked.Increment(ref _dateRequestVersion);
        _unavailableDateMessage = null;
        SelectedDate = null;
        SelectedTimeOption = null;
        AvailableTimeOptions.Clear();
        OnPropertyChanged(nameof(HasSelectedDate));
        OnPropertyChanged(nameof(DateDisplayText));

        if (resetPickerToToday)
        {
            _isResettingPickerDate = true;
            PickerDate = DateTime.Today;
            _isResettingPickerDate = false;
        }
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
                SelectedServiceType,
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
            SelectedTimeOption = null;
            AvailableTimeOptions.Clear();
            OnPropertyChanged(nameof(HasSelectedDate));
            OnPropertyChanged(nameof(DateDisplayText));

            await RegenerateTimeOptionsAsync(date.Date, subService, requestVersion);
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
        SelectedTimeOption = null;
        AvailableTimeOptions.Clear();
        OnPropertyChanged(nameof(HasSelectedDate));
        OnPropertyChanged(nameof(DateDisplayText));

        _isResettingPickerDate = true;
        PickerDate = DateTime.Today;
        _isResettingPickerDate = false;
    }

    private void ResetSelections()
    {
        _isResettingSelections = true;
        SelectedDentalService = string.Empty;
        SelectedMedicalReason = string.Empty;
        SelectedFollowUpReason = string.Empty;
        SelectedCertificateType = string.Empty;
        OtherReasonText = string.Empty;
        AdditionalNotes = string.Empty;
        CertificatePurpose = string.Empty;
        _isResettingSelections = false;

        ClearDateSelection(resetPickerToToday: false);
        _isResettingPickerDate = true;
        PickerDate = DateTime.Today.AddDays(1);
        _isResettingPickerDate = false;
    }

    private string GetCurrentSubService()
    {
        if (IsMedical)
            return SelectedMedicalReason;
        if (IsDental)
            return SelectedDentalService;
        if (IsFollowUp)
            return SelectedFollowUpReason;
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

    private async Task RegenerateTimeOptionsAsync(
        DateTime date,
        string subService,
        int requestVersion)
    {
        var timeOptions = await Services.ClinicScheduleService.Instance
            .GetAvailableTimeOptionsAsync(date, SelectedServiceType, subService);

        var bookedTimes = await Services.AppointmentService.Instance
            .GetBookedTimesAsync(date, SelectedServiceType, subService);

        if (requestVersion != Volatile.Read(ref _dateRequestVersion) ||
            SelectedDate?.Date != date.Date ||
            !string.Equals(subService, GetCurrentSubService(), StringComparison.Ordinal))
        {
            return;
        }

        AvailableTimeOptions.Clear();
        SelectedTimeOption = null;

        foreach (var option in timeOptions)
        {
            if (!bookedTimes.Any(booked =>
                    string.Equals(booked, option, StringComparison.OrdinalIgnoreCase)))
            {
                AvailableTimeOptions.Add(option);
            }
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
            if (string.IsNullOrWhiteSpace(SelectedFollowUpReason))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please select a follow-up reason.", "OK");
                return;
            }

            subService = SelectedFollowUpReason;
            reasonOrPurpose = AdditionalNotes;
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

            if (!SelectedDate.HasValue || string.IsNullOrWhiteSpace(SelectedTimeOption))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please select a date and time.", "OK");
                return;
            }

            string? scheduleError;
            try
            {
                scheduleError = await Services.ClinicScheduleService.Instance.ValidateSlotAsync(
                    SelectedDate.Value,
                    SelectedServiceType,
                    subService,
                    SelectedTimeOption);
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

        bool proceed = await Shell.Current.DisplayAlert(
            "Confirm Appointment Request?",
            "Review your appointment details before submitting your request.",
            "Confirm",
            "Cancel");

        if (!proceed)
            return;

        var appointment = new Models.Appointment
        {
            PatientEmail = user.Email,
            ServiceType = SelectedServiceType,
            SubService = subService,
            ReasonOrPurpose = reasonOrPurpose,
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


