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

    [ObservableProperty]
    private DateTime? expiresOn;

    [RelayCommand]
    private async Task Post()
    {
        if (string.IsNullOrWhiteSpace(Message))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter an announcement message.", "OK");
            return;
        }

        var announcement = new Models.Announcement
        {
            Message = Message,
            DatePosted = DateTime.Now,
            ExpiresOn = ExpiresOn,
            IsActive = true,
            TargetRole = SelectedTargetRole
        };

        await Services.AnnouncementService.Instance.AddAsync(announcement);

        await Shell.Current.DisplayAlert("Posted", "Announcement has been posted.", "OK");
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}