using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace USJR_eCLINIC.ViewModels;

public partial class LoginViewModel :
    ObservableObject
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
    private bool rememberMe= true;

    [ObservableProperty]
    private bool isLoggingIn;

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
                await Services.SecureSessionService
                    .Instance
                    .CreateSessionAsync(user);

                var shouldOfferBiometric =
                    Services
                        .BiometricAuthenticationService
                        .Instance
                        .IsSupported &&
                    await Services
                        .BiometricPreferenceService
                        .Instance
                        .ShouldOfferSetupAsync(
                            user.Id);

                if (shouldOfferBiometric)
                {
                    var enableBiometric =
                        await Shell.Current.DisplayAlert(
                            "Enable biometric login?",
                            "Use your fingerprint to " +
                            "quickly access USJ-R " +
                            "e-Clinic next time.",
                            "Enable",
                            "Not Now");

                    if (enableBiometric)
                    {
                        var biometricResult =
                            await Services
                                .BiometricAuthenticationService
                                .Instance
                                .AuthenticateAsync();

                        if (biometricResult
                            .IsSuccessful)
                        {
                            await Services
                                .BiometricPreferenceService
                                .Instance
                                .EnableAsync(
                                    user.Id);

                            await Shell.Current
                                .DisplayAlert(
                                    "Biometric Login Enabled",
                                    "You can use your " +
                                    "fingerprint to unlock " +
                                    "USJ-R e-Clinic next time.",
                                    "OK");
                        }
                        else
                        {
                            await Shell.Current
                                .DisplayAlert(
                                    "Biometric Setup Not Completed",
                                    "Your fingerprint could " +
                                    "not be verified. You can " +
                                    "continue using your password.",
                                    "OK");
                        }
                    }
                    else
                    {
                        await Services
                            .BiometricPreferenceService
                            .Instance
                            .DeclineAsync(
                                user.Id);
                    }
                }
            }
            else
            {
                await Services.SecureSessionService
                    .Instance
                    .ClearSessionAsync();
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