using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class NotificationListItem : ObservableObject
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    [ObservableProperty] private bool isRead;
}

public partial class NotificationsViewModel : ObservableObject
{
    private readonly List<NotificationListItem> _allNotifications = new();
    public ObservableCollection<NotificationListItem> Notifications { get; } = new();

    [ObservableProperty] private bool hasNotifications;
    [ObservableProperty] private string selectedFilter = "All";
    [ObservableProperty] private int unreadNotificationCount;

    public bool IsAllSelected => SelectedFilter == "All";
    public bool IsUnreadSelected => SelectedFilter == "Unread";
    public string UnreadFilterLabel => UnreadNotificationCount == 0 ? "Unread" : $"Unread ({UnreadNotificationCount})";

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null)
            return;

        var items = await Services.NotificationService.Instance.GetForUserAsync(user.Email);
        _allNotifications.Clear();
        foreach (var notification in items)
        {
            _allNotifications.Add(new NotificationListItem
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                DateDisplay = notification.DateCreated.ToString("MMM dd, yyyy · h:mm tt"),
                IsRead = notification.IsRead
            });
        }

        UnreadNotificationCount = _allNotifications.Count(item => !item.IsRead);
        OnPropertyChanged(nameof(UnreadFilterLabel));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        IEnumerable<NotificationListItem> source = SelectedFilter == "Unread"
            ? _allNotifications.Where(item => !item.IsRead)
            : _allNotifications;

        Notifications.Clear();
        foreach (var notification in source)
            Notifications.Add(notification);

        HasNotifications = Notifications.Count > 0;
    }

    [RelayCommand]
    private void SelectFilter(string filter)
    {
        SelectedFilter = filter == "Unread" ? "Unread" : "All";
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsUnreadSelected));
        ApplyFilter();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task OpenNotification(NotificationListItem item)
    {
        if (item == null)
            return;

        var user = Services.AuthService.Instance.CurrentUser;
        if (user != null && !item.IsRead)
        {
            var markedRead = await Services.NotificationService.Instance.MarkAsReadAsync(item.Id, user.Email);
            if (markedRead)
            {
                item.IsRead = true;
                UnreadNotificationCount = Math.Max(0, UnreadNotificationCount - 1);
                OnPropertyChanged(nameof(UnreadFilterLabel));
                ApplyFilter();
            }
        }

        await Shell.Current.Navigation.PushAsync(
            new Views.NotificationDetailPage(item.Title, item.Message, item.DateDisplay));
    }

    [RelayCommand]
    private async Task DeleteNotification(NotificationListItem item)
    {
        if (item == null)
            return;

        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Notification?",
            "This will move the notification to trash. It will be permanently deleted after 30 days.",
            "Delete",
            "Cancel");
        if (!confirm)
            return;

        await Services.NotificationService.Instance.SoftDeleteAsync(item.Id);
        await RefreshAsync();
    }
}
