using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace USJR_eCLINIC.ViewModels;

public partial class NotificationDetailViewModel : ObservableObject
{
    [ObservableProperty] private string title = string.Empty;
    [ObservableProperty] private string message = string.Empty;
    [ObservableProperty] private string dateDisplay = string.Empty;

    public NotificationDetailViewModel(string title, string message, string dateDisplay)
    {
        Title = title;
        Message = message;
        DateDisplay = dateDisplay;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}