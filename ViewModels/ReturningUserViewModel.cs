using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ReturningUserViewModel : ObservableObject
{
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
    private bool isLoading;

    [ObservableProperty]
    private bool hasRememberedUser;

    public async Task InitializeAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            var user = await Services.SecureSessionService.Instance
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

            UserName = user.FullName;
            Role = user.Role;
            ProfileImagePath = user.ProfileImagePath ?? string.Empty;

            IdLabel = user.Role switch
            {
                "Student" => "Student ID",
                "Faculty" or "Admin Personnel" or "Non-Teaching" => "Employee ID",
                "R.E.A.D.S. Scholar" => "Scholar ID",
                _ => "Account ID"
            };

            MaskedId = MaskIdNumber(user.IdNumber);
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
        await Shell.Current.Navigation.PushAsync(new Views.LoginPage());
    }

    [RelayCommand]
    private async Task UseAnotherAccount()
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Use another account?",
            "The remembered account will be removed from this device.",
            "Continue",
            "Cancel");

        if (!confirmed)
            return;

        await Services.SecureSessionService.Instance.ClearSessionAsync();
        Services.AuthService.Instance.Logout();

        await Shell.Current.Navigation.PopToRootAsync();
        await Shell.Current.Navigation.PushAsync(new Views.LoginPage());
    }

    private static string MaskIdNumber(string? idNumber)
    {
        if (string.IsNullOrWhiteSpace(idNumber))
            return "Not available";

        var normalized = idNumber.Trim();
        var hyphenIndex = normalized.IndexOf('-');

        if (hyphenIndex > 0 && hyphenIndex < normalized.Length - 1)
        {
            var prefix = normalized[..hyphenIndex];
            var numberPart = normalized[(hyphenIndex + 1)..];

            if (numberPart.Length == 1)
                return $"{prefix}-****";

            if (numberPart.Length <= 4)
            {
                return $"{prefix}-" +
                       new string('*', numberPart.Length - 1) +
                       numberPart[^1];
            }

            return $"{prefix}-" +
                   new string('*', numberPart.Length - 2) +
                   numberPart[^2..];
        }

        if (normalized.Length <= 4)
            return new string('*', normalized.Length);

        return normalized[0] +
               new string('*', normalized.Length - 3) +
               normalized[^2..];
    }
}
