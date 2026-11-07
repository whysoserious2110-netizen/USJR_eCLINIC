using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentistAppointmentItem : ObservableObject
{
    public string PatientName { get; set; } = string.Empty;
    public string PatientIdNumber { get; set; } = string.Empty;
    public string PatientIdDisplay => string.IsNullOrWhiteSpace(PatientIdNumber) ? "ID unavailable" : $"ID: {PatientIdNumber}";
    public string DentalService { get; set; } = string.Empty;
    public string ReasonOrPurpose { get; set; } = string.Empty;
    public bool HasReasonOrPurpose => !string.IsNullOrWhiteSpace(ReasonOrPurpose);
    public bool IsFollowUp { get; set; }
    public DateTime VisitDate { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
}

public partial class DentistAppointmentsViewModel : ObservableObject
{
    private readonly List<DentistAppointmentItem> _all = new();
    private readonly bool _followUpsOnly;

    public ObservableCollection<DentistAppointmentItem> Appointments { get; } = new();

    [ObservableProperty] private bool hasAppointments;
    [ObservableProperty] private string selectedTab = "Upcoming";
    [ObservableProperty] private string headerTitle = "Dental Appointments";
    [ObservableProperty] private string headerSubtitle = "Requests and visit history";
    [ObservableProperty] private string emptyMessage = "No dental appointments here.";

    public bool IsAllTab => SelectedTab == "All";
    public bool IsUpcomingTab => SelectedTab == "Upcoming";
    public bool IsPastTab => SelectedTab == "Past";

    public DentistAppointmentsViewModel(bool followUpsOnly = false)
    {
        _followUpsOnly = followUpsOnly;
        if (_followUpsOnly)
        {
            HeaderTitle = "Follow-up Visits";
            HeaderSubtitle = "Scheduled dental follow-up appointments";
            EmptyMessage = "No upcoming dental follow-up visits.";
        }
    }

    public async Task RefreshAsync()
    {
        var dentalAppointments = await Services.AppointmentService.Instance.GetAllForServiceAsync("Dental");
        var certificateAppointments = await Services.AppointmentService.Instance.GetAllForServiceAsync("Cert Request");
        var appointments = dentalAppointments
            .Concat(certificateAppointments.Where(appointment => appointment.SubService == "Dental Certificate"))
            .OrderByDescending(appointment => Services.AppointmentService.GetVisitDateTime(appointment))
            .ToList();

        _all.Clear();
        foreach (var appointment in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appointment.PatientEmail);
            var isFollowUp = appointment.ServiceType == "Dental" &&
                             string.Equals(appointment.SubService, "Dental Follow-up", StringComparison.OrdinalIgnoreCase);

            _all.Add(new DentistAppointmentItem
            {
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                PatientIdNumber = patient?.IdNumber ?? string.Empty,
                DentalService = appointment.ServiceType == "Cert Request" ? "Dental Certificate Visit" : appointment.SubService,
                ReasonOrPurpose = appointment.ReasonOrPurpose,
                IsFollowUp = isFollowUp,
                VisitDate = appointment.VisitDate,
                DateDisplay = appointment.VisitDate.ToString("MMM dd, yyyy"),
                TimeDisplay = appointment.VisitTime,
                Status = appointment.Status,
                StatusColor = appointment.Status switch
                {
                    "Confirmed" => Color.FromArgb("#2C9057"),
                    "VitalsRecorded" => Color.FromArgb("#087A3D"),
                    "CheckedIn" => Color.FromArgb("#3B78B4"),
                    "Completed" => Color.FromArgb("#67766D"),
                    "Cancelled" or "Expired" or "Missed" => Color.FromArgb("#C84D4D"),
                    _ => Color.FromArgb("#C18A20")
                }
            });
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var today = DateTime.Today;
        IEnumerable<DentistAppointmentItem> source = _followUpsOnly
            ? _all.Where(appointment => appointment.IsFollowUp)
            : _all;

        IEnumerable<DentistAppointmentItem> filtered = SelectedTab switch
        {
            "Upcoming" => source
                .Where(appointment =>
                    appointment.VisitDate.Date >= today &&
                    (appointment.Status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded"))
                .OrderBy(appointment => appointment.VisitDate),
            "Past" => source
                .Where(appointment =>
                    appointment.VisitDate.Date < today ||
                    (appointment.Status is "Completed" or "Cancelled" or "Expired" or "Missed"))
                .OrderByDescending(appointment => appointment.VisitDate),
            _ => source.OrderByDescending(appointment => appointment.VisitDate)
        };

        Appointments.Clear();
        foreach (var item in filtered)
            Appointments.Add(item);

        HasAppointments = Appointments.Count > 0;
        EmptyMessage = _followUpsOnly
            ? SelectedTab == "Past"
                ? "No past dental follow-up visits."
                : SelectedTab == "All"
                    ? "No dental follow-up visits have been booked."
                    : "No upcoming dental follow-up visits."
            : SelectedTab == "Past"
                ? "No past dental appointments."
                : SelectedTab == "All"
                    ? "No dental appointments have been booked."
                    : "No upcoming dental appointments.";
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
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}

