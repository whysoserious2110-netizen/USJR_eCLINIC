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

    public ObservableCollection<UpcomingVisitItem> UpcomingVisits { get; } = new();

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        UserName = user.FullName;
        UserRole = user.Role;
        ProfileImageSource = user.ProfileImagePath;

        var idPrefix = user.Role switch
        {
            "Student" => "Student ID",
            "Faculty" or "Admin Personnel" or "Non-Teaching" => "Employee ID",
            "R.E.A.D.S. Scholar" => "Scholar ID",
            _ => "ID"
        };
        IdLabelDisplay = $"{idPrefix}: {user.IdNumber}";

        var announcement = await Services.AnnouncementService.Instance.GetActiveForRoleAsync(user.Role);
        if (announcement != null)
        {
            HasAnnouncement = true;
            AnnouncementText = announcement.Message;
        }
        else
        {
            HasAnnouncement = false;
        }

        await LoadUpcomingVisitsAsync(user.Email);
        UnreadNotificationCount = await Services.NotificationService.Instance.GetUnreadCountAsync(user.Email);
        HasUnreadNotifications = UnreadNotificationCount > 0;

        UnseenAppointmentCount = await Services.AppointmentService.Instance.GetUnseenCountAsync(user.Email);
        HasUnseenAppointments = UnseenAppointmentCount > 0;


    }

    private async Task LoadUpcomingVisitsAsync(string patientEmail)
    {
        var appointments = await Services.AppointmentService.Instance.GetAllForPatientAsync(patientEmail);

        var upcoming = appointments
            .Where(a => a.VisitDate.Date >= DateTime.Today && a.Status != "Cancelled")
            .OrderBy(a => a.VisitDate);

        UpcomingVisits.Clear();

        foreach (var a in upcoming)
        {
            UpcomingVisits.Add(new UpcomingVisitItem
            {
                AppointmentId = a.Id,
                Day = a.VisitDate.Day.ToString(),
                Month = a.VisitDate.ToString("MMM").ToUpper(),
                Title = a.ServiceType,
                SubService = a.SubService,
                Details = $"{a.VisitTime} · {a.Location}",
                Status = a.Status,
                StatusColor = a.Status switch
                {
                    "Confirmed" => Color.FromArgb("#0F9B8E"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    _ => Color.FromArgb("#D9A441") // Pending
                }
            });
        }

        HasUpcomingVisits = UpcomingVisits.Count > 0;
    }

    [RelayCommand]
    private async Task BookAppointment()
        => await Shell.Current.Navigation.PushAsync(new Views.BookAppointmentPage());

    [RelayCommand]
    private async Task MedicalRecords()
        => await Shell.Current.Navigation.PushAsync(new Views.MedicalRecordsPage());

    [RelayCommand]
    private async Task DentalRecords()
        => await Shell.Current.Navigation.PushAsync(new Views.DentalRecordsPage());

    [RelayCommand]
    private async Task Prescriptions()
        => await Shell.Current.Navigation.PushAsync(new Views.PrescriptionsPage());

    [RelayCommand]
    private async Task GoToAppointments()
        => await Shell.Current.Navigation.PushAsync(new Views.AppointmentsPage());

    [RelayCommand]
    private async Task GoToNotifications()
        => await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());

    [RelayCommand]
    private async Task GoToProfile()
        => await Shell.Current.Navigation.PushAsync(new Views.ProfilePage());
}