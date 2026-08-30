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
    public string DateDisplay { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanCancel { get; set; }
}

public partial class AppointmentsViewModel : ObservableObject
{
    private List<AppointmentListItem> _allAppointments = new();

    public ObservableCollection<AppointmentListItem> Appointments { get; } = new();

    [ObservableProperty]
    private bool hasAppointments;

    [ObservableProperty]
    private string selectedTab = "Upcoming";

    public bool IsAllTab => SelectedTab == "All";
    public bool IsUpcomingTab => SelectedTab == "Upcoming";
    public bool IsPastTab => SelectedTab == "Past";

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var appts = await Services.AppointmentService.Instance.GetAllForPatientAsync(user.Email);

        _allAppointments = appts.Select(a => new AppointmentListItem
        {
            AppointmentId = a.Id,
            ServiceType = a.ServiceType,
            SubService = a.SubService,
            VisitDate = a.VisitDate,
            DateDisplay = a.VisitDate.ToString("MMM dd, yyyy"),
            TimeDisplay = a.VisitTime,
            Status = a.Status,
            CanCancel = a.Status == "Pending" || a.Status == "Confirmed",
            StatusColor = a.Status switch
            {
                "Confirmed" => Color.FromArgb("#0F9B8E"),
                "Completed" => Color.FromArgb("#4A90D9"),
                "Cancelled" => Color.FromArgb("#E05B5B"),
                _ => Color.FromArgb("#D9A441")
            }
        }).ToList();

        ApplyFilter();

        await Services.AppointmentService.Instance.MarkAllSeenAsync(user.Email);
    }

    private void ApplyFilter()
    {
        var today = DateTime.Today;

        IEnumerable<AppointmentListItem> filtered = SelectedTab switch
        {
            "Upcoming" => _allAppointments.Where(a => a.VisitDate.Date >= today && a.Status != "Cancelled" && a.Status != "Completed")
                                            .OrderBy(a => a.VisitDate),
            "Past" => _allAppointments.Where(a => a.VisitDate.Date < today || a.Status == "Completed" || a.Status == "Cancelled")
                                        .OrderByDescending(a => a.VisitDate),
            _ => _allAppointments.OrderByDescending(a => a.VisitDate)
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
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}