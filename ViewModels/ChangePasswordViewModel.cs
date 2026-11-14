using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject
{
    [ObservableProperty] private string currentPassword = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isBusy;

    [RelayCommand]
    private async Task Change()
    {
        if (IsBusy) return;
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null)
        {
            await Shell.Current.DisplayAlert("Sign in required", "Sign in again to change your password.", "OK");
            return;
        }
        if (string.IsNullOrWhiteSpace(CurrentPassword) || string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            await Shell.Current.DisplayAlert("Missing information", "Enter your current password and complete both new password fields.", "OK");
            return;
        }
        if (!IsStrongPassword(NewPassword))
        {
            await Shell.Current.DisplayAlert("Weak password", "Use at least 8 characters with uppercase, lowercase, number, and special character.", "OK");
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            await Shell.Current.DisplayAlert("Password mismatch", "The new passwords do not match.", "OK");
            return;
        }
        if (CurrentPassword == NewPassword)
        {
            await Shell.Current.DisplayAlert("Choose a different password", "Your new password must differ from your current password.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var changed = await Services.AuthService.Instance.ChangePasswordAsync(user.Id, CurrentPassword, NewPassword);
            if (!changed)
            {
                await Shell.Current.DisplayAlert("Change failed", "The current password is incorrect or the account could not be updated.", "OK");
                return;
            }
            await Services.SecureSessionService.Instance.ClearSessionAsync();
            Services.AuthService.Instance.Logout();
            await Shell.Current.DisplayAlert("Password changed", "Your password is saved securely. Sign in again using the new password.", "Continue");
            await Shell.Current.Navigation.PopToRootAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Cancel() => await Shell.Current.Navigation.PopAsync();

    private static bool IsStrongPassword(string value) => value.Length >= 8 && value.Any(char.IsUpper) &&
        value.Any(char.IsLower) && value.Any(char.IsDigit) && value.Any(c => !char.IsLetterOrDigit(c));
}
