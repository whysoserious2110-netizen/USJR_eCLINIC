using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PostAnnouncementViewModel : ObservableObject
{
    public ObservableCollection<string> TargetRoles { get; } = new()
    {
        "All", "Student", "Faculty", "Admin Personnel", "Non-Teaching", "R.E.A.D.S. Scholar"
    };

    [ObservableProperty]
    private string selectedTargetRole = "All";

    [ObservableProperty]
    private string message = string.Empty;

    [RelayCommand]
    private async Task Post()
    {
        if (Services.AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only the R.E.A.D.S. Scholar clinic student worker can post announcements.",
                "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            await Shell.Current.DisplayAlert(
                "Missing message",
                "Please enter an announcement message.",
                "OK");
            return;
        }

        var announcement = new Models.Announcement
        {
            Message = Message.Trim(),
            DatePosted = DateTime.Now,
            IsActive = true,
            TargetRole = SelectedTargetRole
        };

        var posted = await Services.AnnouncementService.Instance.AddAsync(announcement);
        if (!posted)
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only the R.E.A.D.S. Scholar clinic student worker can post announcements.",
                "OK");
            return;
        }

        Message = string.Empty;
        await Shell.Current.DisplayAlert(
            "Announcement posted",
            "Your announcement is now available to the selected audience.",
            "OK");
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
