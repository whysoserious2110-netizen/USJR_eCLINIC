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
}

public partial class ReadsAppointmentsOverviewViewModel : ObservableObject
{
    private List<ReadsAppointmentOverviewItem> _all = new();

    public ObservableCollection<ReadsAppointmentOverviewItem> Appointments { get; } = new();

    [ObservableProperty] private bool hasAppointments;
    [ObservableProperty] private string selectedTab = "Upcoming";

    public bool IsAllTab => SelectedTab == "All";
    public bool IsUpcomingTab => SelectedTab == "Upcoming";
    public bool IsPastTab => SelectedTab == "Past";

    public async Task RefreshAsync()
    {
        var appointments = await Services.AppointmentService.Instance.GetAllAppointmentsAsync();

        _all.Clear();
        foreach (var appt in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            _all.Add(new ReadsAppointmentOverviewItem
            {
                AppointmentId = appt.Id,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                ServiceType = appt.ServiceType,
                SubService = appt.SubService,
                VisitDate = appt.VisitDate,
                DateDisplay = appt.VisitDate.ToString("MMM dd, yyyy"),
                TimeDisplay = appt.VisitTime,
                Status = appt.Status,
                StatusColor = appt.Status switch
                {
                    "Confirmed" => Color.FromArgb("#0F9B8E"),
                    "CheckedIn" => Color.FromArgb("#2E6FDB"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    "Cancelled" => Color.FromArgb("#E05B5B"),
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
            "Upcoming" => _all.Where(a => a.VisitDate.Date >= today && a.Status != "Cancelled" && a.Status != "Completed")
                               .OrderBy(a => a.VisitDate),
            "Past" => _all.Where(a => a.VisitDate.Date < today || a.Status == "Completed" || a.Status == "Cancelled")
                           .OrderByDescending(a => a.VisitDate),
            _ => _all.OrderByDescending(a => a.VisitDate)
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

        var currentUser =
            Services.AuthService.Instance.CurrentUser;

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
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}