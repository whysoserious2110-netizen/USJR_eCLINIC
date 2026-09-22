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
    private bool rememberMe;

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

        // Passwords must not be trimmed because
        // spaces can be valid password characters.
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

            Models.UserAccount? user;

            var localAccount =
                await Services.AuthService.Instance
                    .GetAccountByIdentifierAsync(
                        input);

            // Seeded staff accounts (Doctor, Nurse, Dentist,
            // R.E.A.D.S. Scholar) continue using the existing
            // local clinic authentication. Student, Faculty,
            // Admin Personnel, and Non-Teaching always
            // authenticate centrally instead.
            if (localAccount != null &&
                !Services.AuthService.CentralPatientRoles
                    .Contains(localAccount.Role))
            {
                var localLoginSuccessful =
                    await Services.AuthService.Instance
                        .LoginAsync(
                            input,
                            enteredPassword);

                if (!localLoginSuccessful)
                {
                    await Shell.Current.DisplayAlert(
                        "Login failed",
                        "Invalid email/ID or password.",
                        "OK");

                    return;
                }

                user =
                    Services.AuthService.Instance
                        .CurrentUser;
            }
            else
            {
                // Student, Faculty, Admin Personnel, and
                // Non-Teaching always authenticate through
                // the central API. The app must never fall
                // back to an old local password for them.
                var apiResult =
                    await Services.ClinicApiService
                        .Instance
                        .LoginPatientAsync(
                            input,
                            enteredPassword);

                if (!apiResult.Authenticated ||
                    apiResult.User == null)
                {
                    await Shell.Current.DisplayAlert(
                        "Login failed",
                        string.IsNullOrWhiteSpace(
                            apiResult.Message)
                            ? "Invalid ID/email " +
                              "or password."
                            : apiResult.Message,
                        "OK");

                    return;
                }

                user =
                    await Services.AuthService.Instance
                        .SignInCentralStudentAsync(
                            apiResult.User);
            }

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
                    Services.AuthService
                        .CentralPatientRoles
                        .Contains(user.Role) &&
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
                "The application could not complete " +
                "the login. Make sure the central " +
                "server is running and try again.",
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