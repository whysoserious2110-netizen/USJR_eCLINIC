using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{

    [RelayCommand]
    private async Task SignIn()
    {
        await Shell.Current.Navigation.PushAsync(new Views.LoginPage());
    }

}