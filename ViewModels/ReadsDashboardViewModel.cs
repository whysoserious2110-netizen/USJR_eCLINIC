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
                a.Status == "Confirmed" &&
                (a.ServiceType == "Medical" ||
                 a.ServiceType == "Dental"))
            .ToList();

        await LoadNextArrivalsAsync(todaysAppointments);
    }

    private async Task LoadNextArrivalsAsync(
        List<Models.Appointment> todaysAppointments)
    {
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

        NextArrivals.Clear();

        foreach (var appointment in confirmedAppointments)
        {
            var patient =
                await Services.AuthService.Instance
                    .GetAccountByEmailAsync(
                        appointment.PatientEmail);

            NextArrivals.Add(new ReadsNextArrivalItem
            {
                AppointmentId = appointment.Id,

                PatientEmail =
                    appointment.PatientEmail,

                PatientName =
                    patient?.FullName ??
                    appointment.PatientEmail,

                PatientIdNumber =
                    patient?.IdNumber ??
                    "No ID number",

                TimeDisplay =
                    appointment.VisitTime,

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
                $"Service: {item.ServiceType}",
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
            new Views.ProfilePage());
    }
}