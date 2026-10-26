using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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
    public string Location { get; set; } = string.Empty;
    public string ReasonOrPurpose { get; set; } = string.Empty;
    public bool HasReasonOrPurpose => !string.IsNullOrWhiteSpace(ReasonOrPurpose);
    public string DetailTitle => string.IsNullOrWhiteSpace(SubService) ? ServiceType : SubService;
    public bool IsCertificateRequest { get; set; }
    public string CertificateStatus { get; set; } = string.Empty;
    public string CertificateDecisionNote { get; set; } = string.Empty;
    public bool HasCertificateDecisionNote => !string.IsNullOrWhiteSpace(CertificateDecisionNote);
    public string CertificateTrackingMessage => CertificateStatus switch
    {
        "Awaiting Appointment Approval" => "R.E.A.D.S. staff are reviewing the appointment date and time.",
        "Awaiting Clinic Visit" => "Your appointment is confirmed. Attend the clinic at the scheduled time.",
        "Visit In Progress" => "Your clinic visit is in progress. The responsible clinician will review the request.",
        "Awaiting Clinical Review" => "Your visit is complete. The responsible clinician is reviewing the certificate request.",
        "Ready for Pickup" => "Your paper certificate is ready. Visit the clinic to collect it.",
        "Declined" => "The clinician declined the request. Review the note below or contact the clinic.",
        "Collected" => "The paper certificate has been marked as collected.",
        "Expired" => "The request expired because the appointment time passed before it was approved.",
        "Missed Appointment" => "The confirmed appointment passed without check-in. Submit a new request if you still need the certificate.",
        "Cancelled" => "The certificate appointment was cancelled.",
        _ => "Certificate request status is not available."
    };
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanCancel { get; set; }
    public bool HasElapsedNotice => Status == "Expired" || Status == "Missed";
    public string ElapsedNotice => Status == "Expired"
        ? "This request expired because its scheduled time passed before it was approved. Submit a new request for another available time."
        : "The scheduled appointment time passed before check-in. This visit is marked missed and cannot be checked in.";
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
            VisitDate = a.VisitDate,
            VisitDateTime = Services.AppointmentService.GetVisitDateTime(a),
            DateDisplay = a.VisitDate.ToString("MMM dd, yyyy"),
            TimeDisplay = string.IsNullOrWhiteSpace(a.VisitTime) ? "To be arranged" : a.VisitTime,
            Location = a.Location,
            ReasonOrPurpose = a.ReasonOrPurpose,
            Status = a.Status,
            CanCancel = a.Status == "Pending" || a.Status == "Confirmed",
            StatusColor = a.Status switch
            {
                "Confirmed" => Color.FromArgb("#0F9B8E"),
                "Completed" => Color.FromArgb("#4A90D9"),
                "Cancelled" => Color.FromArgb("#E05B5B"),
                "Missed" => Color.FromArgb("#C2410C"),
                "Expired" => Color.FromArgb("#7C8794"),
                _ => Color.FromArgb("#D9A441")
            }
        }).ToList();

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

    private void ApplyFilter()
    {
        var now = DateTime.Now;

        IEnumerable<AppointmentListItem> filtered = SelectedTab switch
        {
            "Upcoming" => _allAppointments
                .Where(a => a.VisitDateTime > now &&
                            a.Status != "Cancelled" &&
                            a.Status != "Completed" &&
                            a.Status != "Expired" &&
                            a.Status != "Missed")
                .OrderBy(a => a.VisitDateTime),
            "Past" => _allAppointments
                .Where(a => a.VisitDateTime <= now ||
                            a.Status == "Completed" ||
                            a.Status == "Cancelled" ||
                            a.Status == "Expired" ||
                            a.Status == "Missed")
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



