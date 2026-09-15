using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalQueueItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientRole { get; set; } = string.Empty;
    public string DentalService { get; set; } = string.Empty;
    public string Location { get; set; } = "Main Campus Clinic";
}

public partial class DentistDashboardViewModel : ObservableObject
{
    [ObservableProperty] private string dentistName = string.Empty;
    [ObservableProperty] private string announcementText = string.Empty;
    [ObservableProperty] private bool hasAnnouncement;
    [ObservableProperty] private int todayVisitsCount;
    [ObservableProperty] private int unreadNotificationCount;
    [ObservableProperty] private bool hasUnreadNotifications;
    [ObservableProperty] private string profileImagePath = string.Empty;

    public ObservableCollection<DentalQueueItem> DentalQueue { get; } = new();

    [ObservableProperty] private bool hasQueueItems;

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        DentistName = user.FullName;
        ProfileImagePath = user.ProfileImagePath;

        var announcement = await Services.AnnouncementService.Instance.GetActiveForRoleAsync("Dentist");
        if (announcement != null)
        {
            HasAnnouncement = true;
            AnnouncementText = announcement.Message;
        }
        else
        {
            HasAnnouncement = false;
        }

        var todaysAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Dental");
        TodayVisitsCount = todaysAppointments.Count;

        DentalQueue.Clear();
        foreach (var appt in todaysAppointments.Where(a => a.Status == "Pending" || a.Status == "Confirmed"))
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            DentalQueue.Add(new DentalQueueItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                TimeDisplay = appt.VisitTime,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                PatientRole = patient?.Role ?? string.Empty,
                DentalService = appt.SubService,
                Location = appt.Location
            });
        }

        HasQueueItems = DentalQueue.Count > 0;

        UnreadNotificationCount = await Services.NotificationService.Instance.GetUnreadCountAsync(user.Email);
        HasUnreadNotifications = UnreadNotificationCount > 0;
    }

    [RelayCommand]
    private async Task OpenExamination(DentalQueueItem item)
    {
        await Shell.Current.Navigation.PushAsync(new Views.DentalExaminationPage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.PatientRole, item.DentalService));
    }

    [RelayCommand]
    private async Task GoToTodaysVisits()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistTodaysVisitsPage());

    [RelayCommand]
    private async Task GoToMyPatients()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistMyPatientsPage());

    [RelayCommand]
    private async Task GoToPatients()
        => await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage());

    [RelayCommand]
    private async Task GoToAppointments()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage());

    [RelayCommand]
    private async Task GoToNotifications()
        => await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());

    [RelayCommand]
    private async Task GoToProfile()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistProfilePage());
}