using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class TodaysOverviewVisitItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientIdNumber { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string SubService { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public string StatusDisplay => Status switch
    {
        "CheckedIn" => "Checked in",
        "VitalsRecorded" => "Vitals recorded",
        _ => Status
    };

    public Color StatusColor { get; set; } = Colors.Gray;
    public int? QueueNumber { get; set; }
    public bool HasQueueNumber => QueueNumber.HasValue;

    public string QueueNumberDisplay => QueueNumber.HasValue
        ? $"Queue #{QueueNumber.Value}"
        : string.Empty;


}

public partial class TodaysOverviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string todayDisplay = string.Empty;

    [ObservableProperty]
    private int pendingApprovalCount;

    [ObservableProperty]
    private int todayAppointmentCount;

    [ObservableProperty]
    private int checkedInTodayCount;

    [ObservableProperty]
    private int completedTodayCount;

    [ObservableProperty]
    private bool hasVisits;

    public bool HasPendingApprovals => PendingApprovalCount > 0;

    public ObservableCollection<TodaysOverviewVisitItem> Visits { get; } = new();

    public async Task RefreshAsync()
    {
        TodayDisplay = DateTime.Today.ToString("dddd, MMMM dd, yyyy");

        // This count covers pending requests on all dates.
        PendingApprovalCount = await Services.AppointmentService.Instance
            .GetPendingApprovalCountAsync();
        OnPropertyChanged(nameof(HasPendingApprovals));

        var allAppointments = await Services.AppointmentService.Instance
            .GetAllAppointmentsAsync();

        var todaysVisits = allAppointments
            .Where(appointment =>
                appointment.VisitDate.Date == DateTime.Today &&
                (appointment.Status == "Confirmed" ||
                 appointment.Status == "CheckedIn" ||
                 appointment.Status == "VitalsRecorded" ||
                 appointment.Status == "Completed"))
            .OrderBy(appointment =>
            {
                return DateTime.TryParse(appointment.VisitTime, out var time)
                    ? time.TimeOfDay
                    : TimeSpan.MaxValue;
            })
            .ToList();

        TodayAppointmentCount = todaysVisits.Count;

        CheckedInTodayCount = todaysVisits.Count(appointment =>
            appointment.Status == "CheckedIn" ||
            appointment.Status == "VitalsRecorded");

        CompletedTodayCount = todaysVisits.Count(appointment =>
            appointment.Status == "Completed");

        Visits.Clear();

        foreach (var appointment in todaysVisits)
        {
            var patient = await Services.AuthService.Instance
                .GetAccountByEmailAsync(appointment.PatientEmail);

            Visits.Add(new TodaysOverviewVisitItem
            {
                AppointmentId = appointment.Id,
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                PatientIdNumber = string.IsNullOrWhiteSpace(patient?.IdNumber)
                    ? "No ID number"
                    : patient.IdNumber,
                TimeDisplay = appointment.VisitTime,
                ServiceType = appointment.ServiceType,
                SubService = appointment.SubService,
                Status = appointment.Status,
                StatusColor = appointment.Status switch
                {
                    "Confirmed" => Color.FromArgb("#0F9B8E"),
                    "CheckedIn" => Color.FromArgb("#2E6FDB"),
                    "VitalsRecorded" => Color.FromArgb("#7B61C8"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    _ => Color.FromArgb("#8793A0")
                },
                QueueNumber = appointment.QueueNumber
            });
        }

        HasVisits = Visits.Count > 0;
    }

    [RelayCommand]
    private async Task RefreshDashboard()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ReviewPendingRequests()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.ReadsAppointmentsOverviewPage());
    }

    [RelayCommand]
    private async Task GoToAppointments()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.ReadsAppointmentsOverviewPage());
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}
