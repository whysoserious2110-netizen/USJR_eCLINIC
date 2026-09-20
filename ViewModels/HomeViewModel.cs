using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class UpcomingVisitItem : ObservableObject
{
    public int AppointmentId { get; set; }

    public string Day { get; set; } = string.Empty;

    public string Month { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string SubService { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Color StatusColor { get; set; } = Colors.Gray;
}

public partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string greeting = "Welcome!";

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string userRole = string.Empty;

    [ObservableProperty]
    private string idLabelDisplay = string.Empty;

    [ObservableProperty]
    private string profileImageSource = string.Empty;

    [ObservableProperty]
    private string announcementText = string.Empty;

    [ObservableProperty]
    private bool hasAnnouncement;

    [ObservableProperty]
    private bool hasUpcomingVisits;

    [ObservableProperty]
    private int unreadNotificationCount;

    [ObservableProperty]
    private bool hasUnreadNotifications;

    [ObservableProperty]
    private int unseenAppointmentCount;

    [ObservableProperty]
    private bool hasUnseenAppointments;

    [ObservableProperty]
    private bool canViewApeResults;

    public ObservableCollection<UpcomingVisitItem>
        UpcomingVisits
    { get; } = new();

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;

        if (user == null)
            return;

        Greeting = GetGreeting();

        UserName = user.FullName;
        UserRole = user.Role;
        ProfileImageSource =
            user.ProfileImagePath ?? string.Empty;

        var idPrefix = user.Role switch
        {
            "Student" => "Student ID",

            "Faculty" or
            "Admin Personnel" or
            "Non-Teaching" => "Employee ID",

            "R.E.A.D.S. Scholar" => "Scholar ID",

            _ => "ID"
        };

        IdLabelDisplay =
            $"{idPrefix}: {user.IdNumber}";

        CanViewApeResults =
            user.Role == "Faculty" ||
            user.Role == "Admin Personnel" ||
            user.Role == "Non-Teaching";

        await LoadAnnouncementAsync(user.Role);
        await LoadUpcomingVisitsAsync(user.Email);
        await LoadNotificationSummaryAsync(user.Email);
        await LoadAppointmentSummaryAsync(user.Email);
    }

    private static string GetGreeting()
    {
        var hour = DateTime.Now.Hour;

        return hour switch
        {
            < 11 => "Good morning,",
            < 18=> "Good afternoon,",
            _ => "Good evening,"
        };
    }

    private async Task LoadAnnouncementAsync(
        string userRole)
    {
        var announcement =
            await Services.AnnouncementService.Instance
                .GetActiveForRoleAsync(userRole);

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
        string patientEmail)
    {
        UnreadNotificationCount =
            await Services.NotificationService.Instance
                .GetUnreadCountAsync(patientEmail);

        HasUnreadNotifications =
            UnreadNotificationCount > 0;
    }

    private async Task LoadAppointmentSummaryAsync(
        string patientEmail)
    {
        UnseenAppointmentCount =
            await Services.AppointmentService.Instance
                .GetUnseenCountAsync(patientEmail);

        HasUnseenAppointments =
            UnseenAppointmentCount > 0;
    }

    private async Task LoadUpcomingVisitsAsync(
        string patientEmail)
    {
        var appointments =
            await Services.AppointmentService.Instance
                .GetAllForPatientAsync(patientEmail);

        var upcoming = appointments
            .Where(a =>
                a.VisitDate.Date >= DateTime.Today &&
                a.Status != "Cancelled")
            .OrderBy(a => a.VisitDate)
            .ThenBy(a => a.VisitTime)
            .ToList();

        UpcomingVisits.Clear();

        foreach (var appointment in upcoming)
        {
            UpcomingVisits.Add(new UpcomingVisitItem
            {
                AppointmentId = appointment.Id,

                Day =
                    appointment.VisitDate.Day.ToString(),

                Month =
                    appointment.VisitDate
                        .ToString("MMM")
                        .ToUpper(),

                Title =
                    appointment.ServiceType,

                SubService =
                    appointment.SubService,

                Details =
                    $"{appointment.VisitTime} · " +
                    $"{appointment.Location}",

                Status = appointment.Status switch
                {
                    "CheckedIn" => "Checked In",

                    "VitalsRecorded" =>
                        "Vitals Recorded",

                    _ => appointment.Status
                },

                StatusColor = appointment.Status switch
                {
                    "Confirmed" =>
                        Color.FromArgb("#0F9B8E"),

                    "CheckedIn" =>
                        Color.FromArgb("#2E6FDB"),

                    "VitalsRecorded" =>
                        Color.FromArgb("#7B61C8"),

                    "Completed" =>
                        Color.FromArgb("#4A90D9"),

                    "Cancelled" =>
                        Color.FromArgb("#E05B5B"),

                    _ =>
                        Color.FromArgb("#D9A441")
                }
            });
        }

        HasUpcomingVisits =
            UpcomingVisits.Count > 0;
    }

    [RelayCommand]
    private async Task BookAppointment()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.BookAppointmentPage());
    }

    [RelayCommand]
    private async Task MedicalRecords()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.MedicalRecordsPage());
    }

    [RelayCommand]
    private async Task DentalRecords()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.DentalRecordsPage());
    }

    [RelayCommand]
    private async Task Prescriptions()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.PrescriptionsPage());
    }

    [RelayCommand]
    private async Task ApeLabResults()
    {
        var user =
            Services.AuthService.Instance.CurrentUser;

        var isEligible =
            user?.Role == "Faculty" ||
            user?.Role == "Admin Personnel" ||
            user?.Role == "Non-Teaching";

        if (!isEligible)
        {
            await Shell.Current.DisplayAlert(
                "Not available",
                "APE lab results are available only to " +
                "Faculty, Admin Personnel, and " +
                "Non-Teaching Personnel.",
                "OK");

            return;
        }

        await Shell.Current.Navigation.PushAsync(
            new Views.ApeLabResultsPage());
    }

    [RelayCommand]
    private async Task GoToAppointments()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.AppointmentsPage());
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