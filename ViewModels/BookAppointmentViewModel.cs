using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class BookAppointmentViewModel : ObservableObject
{
    private static readonly HashSet<DateTime> SchoolHolidays = new()
    {
        new DateTime(2026, 8, 6),
        new DateTime(2026, 8, 19),
        new DateTime(2026, 8, 21),
        new DateTime(2026, 8, 28),
        new DateTime(2026, 8, 31),
        new DateTime(2026, 9, 9),
        new DateTime(2026, 10, 2),
        new DateTime(2026, 10, 31),
        new DateTime(2026, 11, 1),
        new DateTime(2026, 11, 2),
        new DateTime(2026, 11, 30),
        new DateTime(2026, 12, 8),
        new DateTime(2026, 12, 19),
        new DateTime(2027, 1, 22),
        new DateTime(2027, 1, 23),
    };

    private bool IsSchoolHoliday(DateTime date) => SchoolHolidays.Contains(date.Date);

    // ---- Service Type ----
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

    // ---- Medical ----
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

    // ---- Dental ----
    public ObservableCollection<string> DentalServices { get; } = new()
    {
        "Oral Prophylaxis", "Tooth Restoration", "Tooth Extraction", "Dental Consultation"
    };

    [ObservableProperty]
    private string selectedDentalService = string.Empty;

    public bool IsDentalConsultation => SelectedDentalService == "Dental Consultation";
    public bool IsDentalWithSchedule => IsDental && !string.IsNullOrEmpty(SelectedDentalService) && !IsDentalConsultation;

    // ---- Follow-up ----
    public ObservableCollection<string> FollowUpReasons { get; } = new()
    {
        "Follow-up consultation", "Re-evaluation", "Treatment follow-up", "Medication follow-up", "Other"
    };

    [ObservableProperty]
    private string selectedFollowUpReason = string.Empty;

    [ObservableProperty]
    private string additionalNotes = string.Empty;

    // ---- Certificate Request ----
    public ObservableCollection<string> CertificateTypes { get; } = new()
    {
        "Medical Certificate", "Other Certificate"
    };

    [ObservableProperty]
    private string selectedCertificateType = string.Empty;

    [ObservableProperty]
    private string certificatePurpose = string.Empty;

    // ---- Date ----
    [ObservableProperty]
    private DateTime pickerDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private DateTime? selectedDate;

    public string DateDisplayText => SelectedDate.HasValue ? SelectedDate.Value.ToString("MMM dd, yyyy (ddd)") : "Select a date";

    public bool HasSelectedDate => SelectedDate.HasValue;

    // ---- Time (dropdown) ----
    public ObservableCollection<string> AvailableTimeOptions { get; } = new();

    [ObservableProperty]
    private string? selectedTimeOption;

    public bool ShowDateTimeSection =>
        (IsMedical) ||
        (IsDental && IsDentalWithSchedule) ||
        (IsFollowUp) ||
        (IsCertRequest);

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
        => OnPropertyChanged(nameof(ShowOtherReason));

    partial void OnSelectedDentalServiceChanged(string value)
    {
        OnPropertyChanged(nameof(IsDentalConsultation));
        OnPropertyChanged(nameof(IsDentalWithSchedule));
        OnPropertyChanged(nameof(ShowDateTimeSection));
    }

    partial void OnPickerDateChanged(DateTime value) => _ = HandleDatePickedAsync(value);

    private async Task HandleDatePickedAsync(DateTime date)
    {
        if (!ShowDateTimeSection) return;

        var allowedDays = GetAllowedDays();
        bool isPast = date.Date < DateTime.Today;
        bool isAvailable = allowedDays.Contains(date.DayOfWeek) && !isPast && !IsSchoolHoliday(date);

        if (!isAvailable)
        {
            SelectedDate = null;
            AvailableTimeOptions.Clear();
            SelectedTimeOption = null;
            OnPropertyChanged(nameof(HasSelectedDate));
            OnPropertyChanged(nameof(DateDisplayText));

            string reason = IsSchoolHoliday(date) ? "This date is a school holiday." :
                             isPast ? "Please pick a future date." :
                             "This service is not available on the selected day. Please choose a different date.";

            await Shell.Current.DisplayAlert("Date Not Available", reason, "OK");
            return;
        }

        SelectedDate = date;
        OnPropertyChanged(nameof(HasSelectedDate));
        OnPropertyChanged(nameof(DateDisplayText));

        await RegenerateTimeOptionsAsync(date);
    }

    private void ResetSelections()
    {
        SelectedDentalService = string.Empty;
        SelectedMedicalReason = string.Empty;
        SelectedFollowUpReason = string.Empty;
        SelectedCertificateType = string.Empty;
        SelectedDate = null;
        SelectedTimeOption = null;
        AvailableTimeOptions.Clear();
        PickerDate = DateTime.Today.AddDays(1);
        OnPropertyChanged(nameof(HasSelectedDate));
        OnPropertyChanged(nameof(DateDisplayText));
    }

    [RelayCommand]
    private void SelectServiceType(string type) => SelectedServiceType = type;

    [RelayCommand]
    private void SelectDentalService(string service) => SelectedDentalService = service;

    [RelayCommand]
    private void SelectMedicalReason(string reason) => SelectedMedicalReason = reason;

    [RelayCommand]
    private void SelectFollowUpReason(string reason) => SelectedFollowUpReason = reason;

    [RelayCommand]
    private void SelectCertificateType(string type) => SelectedCertificateType = type;

    // ---- Availability rules ----

    private List<DayOfWeek> GetAllowedDays()
    {
        if (IsMedical)
            return new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

        if (IsDental)
        {
            return SelectedDentalService switch
            {
                "Oral Prophylaxis" => new() { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Saturday },
                "Tooth Restoration" => new() { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday },
                "Tooth Extraction" => new() { DayOfWeek.Friday, DayOfWeek.Saturday },
                _ => new()
            };
        }

        return new() { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };
    }

    private (TimeSpan start, TimeSpan end) GetHoursForDay(DayOfWeek day)
    {
        if (IsMedical)
            return (new TimeSpan(7, 0, 0), new TimeSpan(21, 0, 0));

        if (IsDental)
        {
            return SelectedDentalService switch
            {
                "Oral Prophylaxis" => day == DayOfWeek.Friday
                    ? (new TimeSpan(13, 0, 0), new TimeSpan(20, 0, 0))
                    : day == DayOfWeek.Saturday
                        ? (new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0))
                        : (new TimeSpan(8, 0, 0), new TimeSpan(20, 0, 0)),

                "Tooth Restoration" => day == DayOfWeek.Saturday
                    ? (new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0))
                    : (new TimeSpan(8, 0, 0), new TimeSpan(21, 0, 0)),

                "Tooth Extraction" => day == DayOfWeek.Friday
                    ? (new TimeSpan(8, 0, 0), new TimeSpan(12, 0, 0))
                    : (new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0)),

                _ => (TimeSpan.Zero, TimeSpan.Zero)
            };
        }

        return (new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
    }

    private async Task RegenerateTimeOptionsAsync(DateTime date)
    {
        AvailableTimeOptions.Clear();
        SelectedTimeOption = null;

        var (start, end) = GetHoursForDay(date.DayOfWeek);
        if (start == TimeSpan.Zero && end == TimeSpan.Zero) return;

        var subService = IsDental ? SelectedDentalService
                        : IsMedical ? SelectedMedicalReason
                        : IsFollowUp ? SelectedFollowUpReason
                        : SelectedCertificateType;

        var bookedTimes = await Services.AppointmentService.Instance.GetBookedTimesAsync(date, SelectedServiceType, subService);

        var current = start;
        while (current < end)
        {
            var label = DateTime.Today.Add(current).ToString("h:mm tt");
            var isPastTime = date.Date == DateTime.Today && DateTime.Now.TimeOfDay > current;

            if (!bookedTimes.Contains(label) && !isPastTime)
                AvailableTimeOptions.Add(label);

            current = current.Add(TimeSpan.FromMinutes(30));
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
            if (ShowOtherReason) reasonOrPurpose = OtherReasonText;
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
                await Shell.Current.DisplayAlert("Dental Consultation", "This is a walk-in service. No appointment is required — please visit the clinic during operating hours.", "OK");
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
                await Shell.Current.DisplayAlert("Missing info", "Please select a certificate type.", "OK");
                return;
            }
            subService = SelectedCertificateType;
            reasonOrPurpose = CertificatePurpose;
        }

        if (ShowDateTimeSection)
        {
            if (SelectedDate == null || string.IsNullOrEmpty(SelectedTimeOption))
            {
                await Shell.Current.DisplayAlert("Missing info", "Please select a date and time.", "OK");
                return;
            }
        }

        bool proceed = await Shell.Current.DisplayAlert(
            "Confirm Appointment Request?",
            "Review your appointment details before submitting your request.",
            "Confirm", "Cancel");

        if (!proceed) return;

        var appointment = new Models.Appointment
        {
            PatientEmail = user.Email,
            ServiceType = SelectedServiceType,
            SubService = subService,
            ReasonOrPurpose = reasonOrPurpose,
            VisitDate = SelectedDate ?? DateTime.Today,
            VisitTime = SelectedTimeOption ?? string.Empty,
            Status = "Pending"
        };

        await Services.AppointmentService.Instance.BookAsync(appointment);

        await Shell.Current.DisplayAlert("Appointment Request Sent", "Your appointment request has been successfully submitted.\nStatus: Pending", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}