using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class NurseDashboardViewModel : ObservableObject
{
    [ObservableProperty] private string nurseName = string.Empty;
    [ObservableProperty] private string announcementText = string.Empty;
    [ObservableProperty] private bool hasAnnouncement;
    [ObservableProperty] private int lowStockCount;
    [ObservableProperty] private int pendingDispenseCount;
    [ObservableProperty] private int unreadNotificationCount;
    [ObservableProperty] private bool hasUnreadNotifications;
    [ObservableProperty] private string profileImagePath = string.Empty;
    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        NurseName = user.FullName;

        var announcement = await Services.AnnouncementService.Instance.GetActiveForRoleAsync("Nurse");
        if (announcement != null)
        {
            HasAnnouncement = true;
            AnnouncementText = announcement.Message;
        }
        else
        {
            HasAnnouncement = false;
        }

        var stock = await Services.MedicineStockService.Instance.GetAllAsync();
        LowStockCount = stock.Count(s => s.Quantity <= s.LowStockThreshold);

        var undispensed = await Services.PrescriptionService.Instance.GetClinicGivenUndispensedAsync();
        PendingDispenseCount = undispensed.Count;

        UnreadNotificationCount = await Services.NotificationService.Instance.GetUnreadCountAsync(user.Email);
        HasUnreadNotifications = UnreadNotificationCount > 0;

        ProfileImagePath = user.ProfileImagePath;
    }

    [RelayCommand]
    private async Task GoToPatientRecord()
        => await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage());

    [RelayCommand]
    private async Task GoToInventory()
        => await Shell.Current.Navigation.PushAsync(new Views.MedicineInventoryPage());

    [RelayCommand]
    private async Task GoToDispensing()
        => await Shell.Current.Navigation.PushAsync(new Views.PrescriptionDispensingPage());

    [RelayCommand]
    private async Task GoToApeEncoding()
        => await Shell.Current.Navigation.PushAsync(new Views.ApeLabEncodingPage());

    [RelayCommand]
    private async Task GoToNotifications()
        => await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());

    [RelayCommand]
    private async Task GoToProfile()
    => await Shell.Current.Navigation.PushAsync(new Views.NurseProfilePage());

    [RelayCommand]
    private async Task GoToAppointments()
    => await Shell.Current.Navigation.PushAsync(new Views.NurseAppointmentsPage());

    [RelayCommand]
    private async Task Logout()
    {
        bool confirm = await Shell.Current.DisplayAlert("Log Out", "Are you sure you want to log out?", "Log Out", "Cancel");
        if (!confirm) return;

        Services.AuthService.Instance.Logout();
        await Shell.Current.Navigation.PopToRootAsync();
    }
}