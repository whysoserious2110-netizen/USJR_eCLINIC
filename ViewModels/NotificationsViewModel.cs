using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class NotificationListItem : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    public bool IsRead { get; set; }
}

public partial class NotificationsViewModel : ObservableObject
{
    public ObservableCollection<NotificationListItem> Notifications { get; } = new();

    [ObservableProperty]
    private bool hasNotifications;

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var items = await Services.NotificationService.Instance.GetForUserAsync(user.Email);

        Notifications.Clear();
        foreach (var n in items)
        {
            Notifications.Add(new NotificationListItem
            {
                Title = n.Title,
                Message = n.Message,
                DateDisplay = n.DateCreated.ToString("MMM dd, yyyy · h:mm tt"),
                IsRead = n.IsRead
            });
        }

        HasNotifications = Notifications.Count > 0;

        await Services.NotificationService.Instance.MarkAllAsReadAsync(user.Email);
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}