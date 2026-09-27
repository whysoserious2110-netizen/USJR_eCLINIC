using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class ReadsNextArrivalItem : ObservableObject
{
    public int AppointmentId { get; set; }

    public string PatientEmail { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public string PatientIdNumber { get; set; } = string.Empty;

    public string TimeDisplay { get; set; } = string.Empty;

    public string ArrivalTimeContext { get; set; } = string.Empty;

    public Color ArrivalTimeContextColor { get; set; } = Colors.Gray;

    public string ServiceType { get; set; } = string.Empty;

    public string SubService { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}

public partial class ReadsDashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string staffName = string.Empty;

    [ObservableProperty]
    private string profileImagePath = string.Empty;

    [ObservableProperty]
    private string announcementText = string.Empty;

    [ObservableProperty]
    private bool hasAnnouncement;

    [ObservableProperty]
    private int unreadNotificationCount;

    [ObservableProperty]
    private bool hasUnreadNotifications;

    [ObservableProperty]
    private int pendingApprovalCount;

    [ObservableProperty]
    private bool hasPendingApprovals;

    [ObservableProperty]
    private bool hasNextArrivals;

    [ObservableProperty]
    private string nextArrivalsSummary = "No confirmed visits scheduled today.";

    public ObservableCollection<ReadsNextArrivalItem>
        NextArrivals
    { get; } = new();

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;

        if (user == null)
            return;

        StaffName = user.FullName;
        ProfileImagePath = user.ProfileImagePath ?? string.Empty;

        await LoadAnnouncementAsync();
        await LoadNotificationSummaryAsync(user.Email);
        await LoadDashboardAppointmentsAsync();
    }

    private async Task LoadAnnouncementAsync()
    {
        var announcement =
            await Services.AnnouncementService.Instance
                .GetActiveForRoleAsync("All");

        if (announcement != null)
        {
            HasAnnouncement = true;
            AnnouncementText = announcement.Message;
        }
        else
        {
            HasAnnouncement = false;
            AnnouncementText = string.Empty;
        }
    }

    private async Task LoadNotificationSummaryAsync(
        string userEmail)
    {
        UnreadNotificationCount =
            await Services.NotificationService.Instance
                .GetUnreadCountAsync(userEmail);

        HasUnreadNotifications =
            UnreadNotificationCount > 0;
    }

    private async Task LoadDashboardAppointmentsAsync()
    {
        PendingApprovalCount =
            await Services.AppointmentService.Instance
                .GetPendingApprovalCountAsync();

        HasPendingApprovals =
            PendingApprovalCount > 0;

        var allAppointments =
            await Services.AppointmentService.Instance
                .GetAllAppointmentsAsync();

        var todaysAppointments = allAppointments
            .Where(a =>
                a.VisitDate.Date == DateTime.Today &&
                a.Status == "Confirmed")
            .ToList();

        await LoadNextArrivalsAsync(todaysAppointments);
    }

    private async Task LoadNextArrivalsAsync(
        List<Models.Appointment> todaysAppointments)
    {
        var now = DateTime.Now;

        var confirmedAppointments = todaysAppointments
            .OrderBy(a =>
            {
                if (DateTime.TryParse(
                    a.VisitTime,
                    out var parsedTime))
                {
                    return parsedTime.TimeOfDay;
                }

                return TimeSpan.MaxValue;
            })
            .Take(3)
            .ToList();

        NextArrivalsSummary = todaysAppointments.Count switch
        {
            0 => "No confirmed visits scheduled today.",
            1 => "1 confirmed visit scheduled today.",
            <= 3 => $"{todaysAppointments.Count} confirmed visits scheduled today.",
            _ => $"Showing the next 3 of {todaysAppointments.Count} confirmed visits today."
        };

        NextArrivals.Clear();

        foreach (var appointment in confirmedAppointments)
        {
            var patient =
                await Services.AuthService.Instance
                    .GetAccountByEmailAsync(
                        appointment.PatientEmail);

            var arrivalContext = GetArrivalTimeContext(
                appointment.VisitDate,
                appointment.VisitTime,
                now);

            NextArrivals.Add(new ReadsNextArrivalItem
            {
                AppointmentId = appointment.Id,

                PatientEmail =
                    appointment.PatientEmail,

                PatientName =
                    patient?.FullName ??
                    appointment.PatientEmail,

                PatientIdNumber =
                    string.IsNullOrWhiteSpace(patient?.IdNumber)
                        ? "No ID number"
                        : patient.IdNumber,

                TimeDisplay =
                    appointment.VisitTime,

                ArrivalTimeContext = arrivalContext.label,

                ArrivalTimeContextColor = arrivalContext.color,

                ServiceType =
                    appointment.ServiceType,

                SubService =
                    string.IsNullOrWhiteSpace(
                        appointment.ReasonOrPurpose)
                        ? appointment.SubService
                        : appointment.ReasonOrPurpose,

                Status =
                    appointment.Status
            });
        }

        HasNextArrivals =
            NextArrivals.Count > 0;
    }

    private static (string label, Color color) GetArrivalTimeContext(
        DateTime visitDate,
        string visitTime,
        DateTime now)
    {
        if (!DateTime.TryParse(visitTime, out var parsedTime))
            return ("Scheduled", Color.FromArgb("#8793A0"));

        var appointmentDateTime = visitDate.Date.Add(parsedTime.TimeOfDay);
        var timeUntilAppointment = appointmentDateTime - now;

        if (timeUntilAppointment <= TimeSpan.Zero)
            return ("Overdue", Color.FromArgb("#C54848"));

        if (timeUntilAppointment <= TimeSpan.FromMinutes(30))
            return ("Due soon", Color.FromArgb("#B47B19"));

        if (timeUntilAppointment <= TimeSpan.FromHours(2))
            return ("Coming up", Color.FromArgb("#2E6FDB"));

        return ("Later today", Color.FromArgb("#687585"));
    }

    [RelayCommand]
    private async Task CheckInArrival(
        ReadsNextArrivalItem? item)
    {
        if (item == null)
            return;

        var currentUser =
            Services.AuthService.Instance.CurrentUser;

        if (currentUser?.Role != "R.E.A.D.S. Scholar")
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only R.E.A.D.S. Scholars can check in patients.",
                "OK");

            return;
        }

        var confirmed =
            await Shell.Current.DisplayAlert(
                "Confirm Patient Check-In",
                $"Patient: {item.PatientName}\n" +
                $"ID: {item.PatientIdNumber}\n" +
                $"Time: {item.TimeDisplay}\n" +
                $"Service: {item.ServiceType} - {item.SubService}",
                "Check In",
                "Cancel");

        if (!confirmed)
            return;

        var checkedInAppointment =
            await Services.AppointmentService.Instance
                .CheckInAsync(item.AppointmentId);

        await LoadDashboardAppointmentsAsync();

        var queueNumber =
            checkedInAppointment.QueueNumber?.ToString()
            ?? "Not assigned";

        await Shell.Current.DisplayAlert(
            "Patient Checked In",
            $"{item.PatientName} has been checked in.\n" +
            $"Queue Number: {queueNumber}",
            "OK");
    }

    [RelayCommand]
    private async Task GoToPatientSearch()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.AdminPatientsListPage());
    }

    [RelayCommand]
    private async Task GoToPostAnnouncement()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.PostAnnouncementPage());
    }

    [RelayCommand]
    private async Task GoToTodaysOverview()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.TodaysOverviewPage());
    }

    [RelayCommand]
    private async Task GoToAppointmentsOverview()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.ReadsAppointmentsOverviewPage());
    }

    [RelayCommand]
    private async Task GoToPatientRegistration()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.RegisterPatientPage());
    }

    [RelayCommand]
    private async Task GoToCheckIn()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.CheckInPage());
    }

    [RelayCommand]
    private async Task GoToNotifications()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.NotificationsPage());
    }

    [RelayCommand]
    private async Task GoToProfile()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.ReadsProfilePage());
    }
}
