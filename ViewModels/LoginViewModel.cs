using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string emailOrId = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isPasswordHidden = true;

    [ObservableProperty]
    private string showHideText = "SHOW";

    [ObservableProperty]
    private bool rememberMe;

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
        ShowHideText = IsPasswordHidden ? "SHOW" : "HIDE";
    }

    [RelayCommand]
    
    private async Task LogIn()
    {
        var input = EmailOrId?.Trim() ?? string.Empty;
        var pass = Password?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(pass))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter your email/ID and password.", "OK");
            return;
        }

        var success = await Services.AuthService.Instance.LoginAsync(input, pass);

        if (!success)
        {
            await Shell.Current.DisplayAlert("Login failed", "Invalid email/ID or password.", "OK");
            return;
        }

        var user = Services.AuthService.Instance.CurrentUser!;

        Page destination = user.Role switch
        {
            "Doctor" => new Views.DoctorDashboardPage(),
            "R.E.A.D.S. Scholar" => new Views.ReadsDashboardPage(),
            _ => new Views.HomePage()
        };

        await Shell.Current.Navigation.PushAsync(destination);
    }

    [RelayCommand]
    private async Task ForgotPassword()
    {
        // TODO: navigate to Forgot Password flow
        await Shell.Current.DisplayAlert("Forgot Password", "Password reset flow coming soon.", "OK");
    }

    
    [RelayCommand]
    private async Task SignUp()
    {
        await Shell.Current.Navigation.PushAsync(new Views.SignUpPage());
    }
}