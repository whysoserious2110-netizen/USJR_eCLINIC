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
    private int readyCertificateCount;

    [ObservableProperty]
    private bool hasNextArrivals;

    [ObservableProperty]
    private string nextArrivalsSummary = "No upcoming confirmed visits today.";

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
        ReadyCertificateCount = await Services.AppointmentService.Instance
            .GetReadyCertificatePickupCountAsync();
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

        var now = DateTime.Now;
        var todaysAppointments = allAppointments
            .Where(a =>
                a.VisitDate.Date == now.Date &&
                a.Status == "Confirmed")
            .ToList();

        await LoadNextArrivalsAsync(todaysAppointments, now);
    }

    private async Task LoadNextArrivalsAsync(
        List<Models.Appointment> todaysAppointments,
        DateTime now)
    {
        // "Next Arrivals" should only show appointments that have not started yet.
        // This removes yesterday's records and earlier appointments from today.
        var upcomingAppointments = todaysAppointments
            .Select(appointment => new
            {
                Appointment = appointment,
                AppointmentDateTime = GetAppointmentDateTime(appointment)
            })
            .Where(item =>
                item.AppointmentDateTime.HasValue &&
                item.AppointmentDateTime.Value > now)
            .OrderBy(item => item.AppointmentDateTime)
            .ToList();

        NextArrivalsSummary = upcomingAppointments.Count switch
        {
            0 => "No upcoming confirmed visits today.",
            1 => "1 upcoming confirmed visit today.",
            <= 3 => $"{upcomingAppointments.Count} upcoming confirmed visits today.",
            _ => $"Showing the next 3 of {upcomingAppointments.Count} upcoming confirmed visits today."
        };

        NextArrivals.Clear();

        foreach (var upcoming in upcomingAppointments.Take(3))
        {
            var appointment = upcoming.Appointment;
            var patient =
                await Services.AuthService.Instance
                    .GetAccountByEmailAsync(
                        appointment.PatientEmail);

            var arrivalContext = GetArrivalTimeContext(
                upcoming.AppointmentDateTime!.Value,
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

    private static DateTime? GetAppointmentDateTime(
        Models.Appointment appointment)
    {
        if (!DateTime.TryParse(appointment.VisitTime, out var parsedTime))
            return null;

        return appointment.VisitDate.Date.Add(parsedTime.TimeOfDay);
    }

    private static (string label, Color color) GetArrivalTimeContext(
        DateTime appointmentDateTime,
        DateTime now)
    {
        var timeUntilAppointment = appointmentDateTime - now;

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
    private async Task GoToCertificatePickup()
    {
        await Shell.Current.Navigation.PushAsync(new Views.CertificatesPage());
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

