using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ReturningUserViewModel : ObservableObject
{
    private int _rememberedUserId;

    [ObservableProperty]
    private string welcomeText = "Welcome back!";

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string maskedId = string.Empty;

    [ObservableProperty]
    private string idLabel = "Student ID";

    [ObservableProperty]
    private string role = string.Empty;

    [ObservableProperty]
    private string profileImagePath = string.Empty;

    [ObservableProperty]
    private string unlockButtonText =
        "Unlock with Fingerprint";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasRememberedUser;

    [ObservableProperty]
    private bool isBiometricEnabled;

    public async Task InitializeAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            var user =
                await Services.SecureSessionService.Instance
                    .GetRememberedUserAsync();

            if (user == null)
            {
                HasRememberedUser = false;

                await Shell.Current.DisplayAlert(
                    "Session expired",
                    "Please sign in again.",
                    "OK");

                await Shell.Current.Navigation.PopAsync();
                return;
            }

            _rememberedUserId = user.Id;

            UserName = user.FullName;
            Role = user.Role;

            ProfileImagePath =
                user.ProfileImagePath ?? string.Empty;

            IdLabel = user.Role switch
            {
                "Student" => "Student ID",

                "Faculty" or
                "Admin Personnel" or
                "Non-Teaching" => "Employee ID",

                "R.E.A.D.S. Scholar" => "Scholar ID",

                _ => "Account ID"
            };

            MaskedId = MaskIdNumber(user.IdNumber);

            IsBiometricEnabled =
                await Services.BiometricPreferenceService
                    .Instance
                    .IsEnabledAsync(user.Id);

            UnlockButtonText = IsBiometricEnabled
                ? "Unlock with Fingerprint"
                : "Continue with Password";

            HasRememberedUser = true;
        }
        catch
        {
            HasRememberedUser = false;

            await Shell.Current.DisplayAlert(
                "Session unavailable",
                "The remembered account could not be loaded. " +
                "Please sign in using your password.",
                "OK");

            await Shell.Current.Navigation.PopAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Unlock()
    {
        if (IsLoading)
            return;

        if (!IsBiometricEnabled)
        {
            await Shell.Current.Navigation.PushAsync(
                new Views.LoginPage());

            return;
        }

        if (!Services.BiometricAuthenticationService
            .Instance.IsSupported)
        {
            await Shell.Current.DisplayAlert(
                "Biometric login unavailable",
                "Use your password to continue.",
                "OK");

            await Shell.Current.Navigation.PushAsync(
                new Views.LoginPage());

            return;
        }

        try
        {
            IsLoading = true;

            var biometricResult =
                await Services.BiometricAuthenticationService
                    .Instance
                    .AuthenticateAsync();

            if (biometricResult.Status ==
                Services.BiometricAuthenticationStatus
                    .Cancelled)
            {
                await Shell.Current.Navigation.PushAsync(
                    new Views.LoginPage());

                return;
            }

            if (!biometricResult.IsSuccessful)
            {
                await Shell.Current.DisplayAlert(
                    "Fingerprint not verified",
                    "Your identity could not be verified. " +
                    "Try again or use your password.",
                    "OK");

                return;
            }

            var restored =
                await Services.SecureSessionService.Instance
                    .TryRestoreSessionAsync();

            if (!restored)
            {
                await Shell.Current.DisplayAlert(
                    "Session expired",
                    "Your remembered session is no longer " +
                    "valid. Please sign in using your password.",
                    "OK");

                await Shell.Current.Navigation.PushAsync(
                    new Views.LoginPage());

                return;
            }

            var user =
                Services.AuthService.Instance.CurrentUser;

            if (user == null)
            {
                await Shell.Current.DisplayAlert(
                    "Unable to unlock",
                    "Please sign in using your password.",
                    "OK");

                return;
            }

            await OpenDashboardAsync(user.Role);
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Unable to unlock",
                "Biometric authentication could not be " +
                "completed. Please use your password.",
                "OK");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UsePassword()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.LoginPage());
    }

    [RelayCommand]
    private async Task UseAnotherAccount()
    {
        var confirmed =
            await Shell.Current.DisplayAlert(
                "Use another account?",
                "The remembered account will be removed " +
                "from this device.",
                "Continue",
                "Cancel");

        if (!confirmed)
            return;

        if (_rememberedUserId > 0)
        {
            Services.BiometricPreferenceService.Instance
                .ResetChoice(_rememberedUserId);
        }

        await Services.SecureSessionService.Instance
            .ClearSessionAsync();

        Services.AuthService.Instance.Logout();

        await Shell.Current.Navigation.PopToRootAsync();

        await Shell.Current.Navigation.PushAsync(
            new Views.LoginPage());
    }

    private static async Task OpenDashboardAsync(
        string role)
    {
        Page destination = role switch
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

        await Shell.Current.Navigation.PushAsync(
            destination);
    }

    private static string MaskIdNumber(
        string? idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber))
            return "Not available";

        var normalized = idNumber.Trim();

        var hyphenIndex =
            normalized.IndexOf('-');

        if (hyphenIndex > 0 &&
            hyphenIndex < normalized.Length - 1)
        {
            var prefix =
                normalized[..hyphenIndex];

            var numberPart =
                normalized[(hyphenIndex + 1)..];

            if (numberPart.Length == 1)
                return $"{prefix}-****";

            if (numberPart.Length <= 4)
            {
                return $"{prefix}-" +
                       new string(
                           '*',
                           numberPart.Length - 1) +
                       numberPart[^1];
            }

            return $"{prefix}-" +
                   new string(
                       '*',
                       numberPart.Length - 2) +
                   numberPart[^2..];
        }

        if (normalized.Length <= 4)
            return new string('*', normalized.Length);

        return normalized[0] +
               new string('*', normalized.Length - 3) +
               normalized[^2..];
    }
}