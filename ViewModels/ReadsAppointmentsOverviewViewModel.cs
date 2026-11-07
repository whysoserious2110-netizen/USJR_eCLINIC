using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class ReadsAppointmentOverviewItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string SubService { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;

    public bool CanApprove => Status == "Pending";
    public bool CanDecline => Status == "Pending";

    public string StatusDisplay =>
        Status switch
        {
            "CheckedIn" => "Checked in",
            "VitalsRecorded" => "Vitals recorded",
            "Declined" => "Declined",
            "Expired" => "Expired",
            "Missed" => "Missed",
            _ => Status
        };
}

public partial class ReadsAppointmentsOverviewViewModel : ObservableObject
{
    private List<ReadsAppointmentOverviewItem> _all = new();

    public ObservableCollection<ReadsAppointmentOverviewItem> Appointments { get; } = new();

    [ObservableProperty]
    private bool hasAppointments;

    [ObservableProperty]
    private string selectedTab = "Upcoming";

    public bool IsAllTab => SelectedTab == "All";
    public bool IsUpcomingTab => SelectedTab == "Upcoming";
    public bool IsPastTab => SelectedTab == "Past";

    public async Task RefreshAsync()
    {
        var appointments =
            await Services.AppointmentService.Instance.GetAllAppointmentsAsync();

        _all.Clear();

        foreach (var appointment in appointments)
        {
            var patient =
                await Services.AuthService.Instance.GetAccountByEmailAsync(
                    appointment.PatientEmail);

            _all.Add(new ReadsAppointmentOverviewItem
            {
                AppointmentId = appointment.Id,
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                ServiceType = appointment.ServiceType == "Cert Request"
                    ? "Certificate Request"
                    : appointment.ServiceType,
                SubService = appointment.SubService,
                VisitDate = appointment.VisitDate,
                DateDisplay = appointment.VisitDate.ToString("MMM dd, yyyy"),
                TimeDisplay = appointment.VisitTime,
                Status = appointment.Status,
                StatusColor = appointment.Status switch
                {
                    "Confirmed" => Color.FromArgb("#0F9B8E"),
                    "CheckedIn" => Color.FromArgb("#2E6FDB"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    "Cancelled" => Color.FromArgb("#E05B5B"),
                    "Declined" or "Expired" or "Missed" => Color.FromArgb("#8793A0"),
                    _ => Color.FromArgb("#D9A441")
                }
            });
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var today = DateTime.Today;

        IEnumerable<ReadsAppointmentOverviewItem> filtered = SelectedTab switch
        {
            "Upcoming" => _all
                .Where(a =>
                    a.VisitDate.Date >= today &&
                    a.Status is not ("Cancelled" or "Completed" or "Declined" or "Expired" or "Missed"))
                .OrderBy(a => a.CanApprove ? 0 : 1)
                .ThenBy(a => a.VisitDate)
                .ThenBy(a => a.TimeDisplay),

            "Past" => _all
                .Where(a =>
                    a.VisitDate.Date < today ||
                    a.Status is "Completed" or "Cancelled" or "Declined" or "Expired" or "Missed")
                .OrderByDescending(a => a.VisitDate)
                .ThenBy(a => a.TimeDisplay),

            _ => _all
                .OrderBy(a => a.CanApprove ? 0 : 1)
                .ThenBy(a => a.VisitDate.Date < today ? 1 : 0)
                .ThenBy(a => a.VisitDate)
                .ThenBy(a => a.TimeDisplay)
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
    private async Task ApproveAppointment(
        ReadsAppointmentOverviewItem? item)
    {
        if (item == null)
            return;

        var currentUser = Services.AuthService.Instance.CurrentUser;

        if (currentUser?.Role != "R.E.A.D.S. Scholar")
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only R.E.A.D.S. Scholars can approve appointments.",
                "OK");

            return;
        }

        if (!item.CanApprove)
        {
            await Shell.Current.DisplayAlert(
                "Not available",
                "Only pending appointments can be approved.",
                "OK");

            return;
        }

        var confirmed = await Shell.Current.DisplayAlert(
            "Approve Appointment?",
            $"Approve {item.PatientName}'s " +
            $"{item.ServiceType} appointment?",
            "Approve",
            "Cancel");

        if (!confirmed)
            return;

        var approved = await Services.AppointmentService.Instance
            .ApproveAsync(item.AppointmentId);

        if (!approved)
        {
            await Shell.Current.DisplayAlert(
                "Error",
                "The appointment could not be approved.",
                "OK");

            return;
        }

        await RefreshAsync();

        await Shell.Current.DisplayAlert(
            "Approved",
            "The appointment has been approved.",
            "OK");
    }

    [RelayCommand]
    private async Task DeclineAppointment(ReadsAppointmentOverviewItem? item)
    {
        if (item == null || !item.CanDecline)
            return;

        if (Services.AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only R.E.A.D.S. Scholars can review clinic appointment requests.",
                "OK");
            return;
        }

        var reason = await Shell.Current.DisplayPromptAsync(
            "Decline Request",
            "Enter a short reason for the patient:",
            "Decline",
            "Cancel",
            "e.g., requested time is unavailable",
            maxLength: 160);

        if (string.IsNullOrWhiteSpace(reason))
            return;

        var declined = await Services.AppointmentService.Instance
            .DeclinePendingAsync(item.AppointmentId, reason);

        if (!declined)
        {
            await Shell.Current.DisplayAlert(
                "Request not updated",
                "This request may have changed or its appointment time has passed. Refresh and review it again.",
                "OK");
            await RefreshAsync();
            return;
        }

        await RefreshAsync();
        await Shell.Current.DisplayAlert(
            "Request declined",
            "The patient will receive the reason in their notifications.",
            "OK");
    }
    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}