using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace USJR_eCLINIC.ViewModels;

public partial class LoginViewModel :
    ObservableObject
{
    private const string RememberLoginIdKey =
        "usjr_eclinic_remember_login_id";

    private const string SavedLoginIdKey =
        "usjr_eclinic_saved_login_id";

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

    [ObservableProperty]
    private bool isLoggingIn;

    public LoginViewModel()
    {
        RememberMe =
            Microsoft.Maui.Storage.Preferences.Default.Get(
                RememberLoginIdKey,
                false);

        if (RememberMe)
        {
            EmailOrId =
                Microsoft.Maui.Storage.Preferences.Default.Get(
                    SavedLoginIdKey,
                    string.Empty);
        }
    }

    partial void OnRememberMeChanged(bool value)
    {
        Microsoft.Maui.Storage.Preferences.Default.Set(
            RememberLoginIdKey,
            value);

        if (!value)
        {
            Microsoft.Maui.Storage.Preferences.Default.Remove(
                SavedLoginIdKey);
        }
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden =
            !IsPasswordHidden;

        ShowHideText =
            IsPasswordHidden
                ? "SHOW"
                : "HIDE";
    }

    [RelayCommand]
    private async Task LogIn()
    {
        if (IsLoggingIn)
            return;

        var input =
            EmailOrId?.Trim() ??
            string.Empty;

        var enteredPassword =
            Password ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(input) ||
            string.IsNullOrEmpty(enteredPassword))
        {
            await Shell.Current.DisplayAlert(
                "Missing information",
                "Please enter your email/ID and password.",
                "OK");

            return;
        }

        try
        {
            IsLoggingIn = true;

            var loginSuccessful =
                await Services.AuthService.Instance
                    .LoginAsync(
                        input,
                        enteredPassword);

            if (!loginSuccessful)
            {
                await Shell.Current.DisplayAlert(
                    "Login failed",
                    "Invalid email/ID or password.",
                    "OK");

                return;
            }

            var user =
                Services.AuthService.Instance.CurrentUser;

            if (user == null)
            {
                await Shell.Current.DisplayAlert(
                    "Login failed",
                    "The account could not be loaded.",
                    "OK");

                return;
            }

            if (RememberMe)
            {
                Microsoft.Maui.Storage.Preferences.Default.Set(
                    SavedLoginIdKey,
                    input);
            }
            else
            {
                Microsoft.Maui.Storage.Preferences.Default.Remove(
                    SavedLoginIdKey);
            }

            Password = string.Empty;

            Page destination =
                user.Role switch
                {
                    "Doctor" =>
                        new Views.DoctorDashboardPage(),

                    "R.E.A.D.S. Scholar" =>
                        new Views.ReadsDashboardPage(),

                    "Nurse" =>
                        new Views.NurseDashboardPage(),

                    "Dentist" =>
                        new Views.DentistDashboardPage(),

                    _ =>
                        new Views.HomePage()
                };

            await Shell.Current.Navigation
                .PushAsync(destination);
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Login unavailable",
                "The local account database could not complete the login.",
                "OK");
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private async Task SignUp()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.SignUpPage());
    }

    [RelayCommand]
    private async Task ForgotPassword()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.ForgotPasswordPage());
    }
}
