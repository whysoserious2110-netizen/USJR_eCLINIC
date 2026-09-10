using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ReadsDashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string staffName = string.Empty;

    [ObservableProperty]
    private string announcementText = string.Empty;

    [ObservableProperty]
    private bool hasAnnouncement;

    [ObservableProperty]
    private int unreadNotificationCount;

    [ObservableProperty]
    private bool hasUnreadNotifications;

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        StaffName = user.FullName;

        var announcement = await Services.AnnouncementService.Instance.GetActiveForRoleAsync("All");
        if (announcement != null)
        {
            HasAnnouncement = true;
            AnnouncementText = announcement.Message;
        }
        else
        {
            HasAnnouncement = false;
        }

        UnreadNotificationCount = await Services.NotificationService.Instance.GetUnreadCountAsync(user.Email);
        HasUnreadNotifications = UnreadNotificationCount > 0;
    }

    [RelayCommand]
    private async Task GoToPatientSearch()
        => await Shell.Current.Navigation.PushAsync(new Views.AdminPatientsListPage());


    [RelayCommand]
    private async Task GoToPostAnnouncement()
    => await Shell.Current.Navigation.PushAsync(new Views.PostAnnouncementPage());

    [RelayCommand]
    private async Task GoToTodaysOverview()
        => await Shell.Current.Navigation.PushAsync(new Views.TodaysOverviewPage());

    [RelayCommand]
    private async Task GoToPatientRegistration()
        => await Shell.Current.Navigation.PushAsync(new Views.RegisterPatientPage());

    [RelayCommand]
    private async Task GoToCheckIn()
        => await Shell.Current.Navigation.PushAsync(new Views.CheckInPage());

    [RelayCommand]
    private async Task GoToAppointmentManagement()
        => await Shell.Current.Navigation.PushAsync(new Views.ManageAppointmentsPage());

    [RelayCommand]
    private async Task GoToNotifications()
        => await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());

    [RelayCommand]
    private async Task GoToProfile()
        => await Shell.Current.Navigation.PushAsync(new Views.ProfilePage());
}