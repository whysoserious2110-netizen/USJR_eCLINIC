using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class BookAppointmentViewModel : ObservableObject
{
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

    // ---- Date / Time ----
    public ObservableCollection<DateChip> DateChips { get; } = new();
    public ObservableCollection<TimeChip> TimeChips { get; } = new();

    [ObservableProperty]
    private DateChip? selectedDateChip;

    [ObservableProperty]
    private TimeChip? selectedTimeChip;

    [ObservableProperty]
    private string dateRangeLabel = string.Empty;

    public bool ShowDateTimeSection =>
        (IsMedical) ||
        (IsDental && IsDentalWithSchedule) ||
        (IsFollowUp) ||
        (IsCertRequest);

    public BookAppointmentViewModel()
    {
        RegenerateDateChips();
    }

    partial void OnSelectedServiceTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsMedical));
        OnPropertyChanged(nameof(IsDental));
        OnPropertyChanged(nameof(IsFollowUp));
        OnPropertyChanged(nameof(IsCertRequest));
        OnPropertyChanged(nameof(ShowDateTimeSection));
        ResetSelections();
        RegenerateDateChips();
    }

    partial void OnSelectedMedicalReasonChanged(string value)
        => OnPropertyChanged(nameof(ShowOtherReason));

    partial void OnSelectedDentalServiceChanged(string value)
    {
        OnPropertyChanged(nameof(IsDentalConsultation));
        OnPropertyChanged(nameof(IsDentalWithSchedule));
        OnPropertyChanged(nameof(ShowDateTimeSection));
        RegenerateDateChips();
    }

    private void ResetSelections()
    {
        SelectedDentalService = string.Empty;
        SelectedMedicalReason = string.Empty;
        SelectedFollowUpReason = string.Empty;
        SelectedCertificateType = string.Empty;
        SelectedDateChip = null;
        SelectedTimeChip = null;
        TimeChips.Clear();
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

        // Follow-up and Cert Request: general clinic days
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

        // Follow-up / Cert Request
        return (new TimeSpan(8, 0, 0), new TimeSpan(17, 0, 0));
    }

    private void RegenerateDateChips()
    {
        DateChips.Clear();
        SelectedDateChip = null;
        TimeChips.Clear();

        if (!ShowDateTimeSection)
        {
            DateRangeLabel = string.Empty;
            return;
        }

        var allowedDays = GetAllowedDays();
        if (allowedDays.Count == 0)
        {
            DateRangeLabel = string.Empty;
            return;
        }

        var date = DateTime.Today.AddDays(1);
        int found = 0;

        while (found < 7)
        {
            if (allowedDays.Contains(date.DayOfWeek))
            {
                DateChips.Add(new DateChip
                {
                    Date = date,
                    DayLabel = date.ToString("ddd").ToUpper(),
                    DayNumber = date.Day.ToString()
                });
                found++;
            }
            date = date.AddDays(1);
        }

        if (DateChips.Count > 0)
        {
            var first = DateChips.First().Date;
            var last = DateChips.Last().Date;

            DateRangeLabel = first.Month == last.Month
                ? first.ToString("MMMM yyyy")
                : $"{first:MMMM} - {last:MMMM yyyy}";
        }
    }

    [RelayCommand]
    private async Task SelectDate(DateChip chip)
    {
        foreach (var d in DateChips) d.IsSelected = false;
        chip.IsSelected = true;
        SelectedDateChip = chip;

        await RegenerateTimeChipsAsync(chip.Date);
    }

    private async Task RegenerateTimeChipsAsync(DateTime date)
    {
        TimeChips.Clear();
        SelectedTimeChip = null;

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
            var isPast = date.Date == DateTime.Today && DateTime.Now.TimeOfDay > current;

            TimeChips.Add(new TimeChip
            {
                Time = label,
                IsBooked = bookedTimes.Contains(label) || isPast
            });

            current = current.Add(TimeSpan.FromMinutes(30));
        }
    }

    [RelayCommand]
    private void SelectTime(TimeChip chip)
    {
        if (chip.IsBooked) return;
        foreach (var t in TimeChips) t.IsSelected = false;
        chip.IsSelected = true;
        SelectedTimeChip = chip;
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
        else // Cert Request
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
            if (SelectedDateChip == null || SelectedTimeChip == null)
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
            VisitDate = SelectedDateChip?.Date ?? DateTime.Today,
            VisitTime = SelectedTimeChip?.Time ?? string.Empty,
            Status = "Pending"
        };

        await Services.AppointmentService.Instance.BookAsync(appointment);

        await Shell.Current.DisplayAlert("Appointment Request Sent", "Your appointment request has been successfully submitted.\nStatus: Pending", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}