using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty] private string emailOrId = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private bool isPasswordHidden = true;
    [ObservableProperty] private string showHideText = "SHOW";
    [ObservableProperty] private bool rememberMe;
    [ObservableProperty] private bool isLoggingIn;
    [ObservableProperty] private bool hasRememberedAccount;

    public bool IsPasswordEntryVisible => true;
    public bool IsRememberMeEnabled => !HasRememberedAccount;
    public string RememberMeLabel => "Remember me";

    partial void OnHasRememberedAccountChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPasswordEntryVisible));
        OnPropertyChanged(nameof(IsRememberMeEnabled));
    }

    public async Task LoadRememberedAccountAsync()
    {
        Models.UserAccount? user = null;
        try
        {
            user = await Services.SecureSessionService.Instance.GetRememberedUserAsync();
        }
        catch
        {
            // A failed session lookup falls back to ordinary password sign-in.
        }

        HasRememberedAccount = user != null;
        RememberMe = user != null;
        Password = string.Empty;

        if (user != null)
            EmailOrId = user.Email;
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
        ShowHideText = IsPasswordHidden ? "SHOW" : "HIDE";
    }

    [RelayCommand]
    private async Task LogIn()
    {
        if (IsLoggingIn) return;

        if (HasRememberedAccount && RememberMe && string.IsNullOrWhiteSpace(Password))
        {
            await ResumeRememberedAccountAsync(EmailOrId?.Trim() ?? string.Empty);
            return;
        }

        var input = EmailOrId?.Trim() ?? string.Empty;
        var enteredPassword = Password ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input) || string.IsNullOrEmpty(enteredPassword))
        {
            await Shell.Current.DisplayAlert("Missing information", "Enter your email or ID and password.", "OK");
            return;
        }

        IsLoggingIn = true;
        try
        {
            var auth = Services.AuthService.Instance;
            if (!await auth.LoginAsync(input, enteredPassword))
            {
                await Shell.Current.DisplayAlert("Login failed", "Invalid email/ID or password.", "OK");
                return;
            }
            var user = auth.CurrentUser;
            if (user == null)
            {
                await Shell.Current.DisplayAlert("Login failed", "The account could not be loaded.", "OK");
                return;
            }

            Password = string.Empty;
            if (!user.EmailVerified || user.AccountStatus == "PendingEmailVerification")
            {
                auth.Logout();
                var send = await Services.EmailVerificationService.Instance.StartVerificationAsync(user.Id);
                await Shell.Current.Navigation.PushAsync(new Views.EmailVerificationPage(user.Id, send.Message));
                return;
            }

            if (user.AccountStatus == "PendingStaffIdReview" || user.AccountStatus == "PendingFacultyReview")
            {
                auth.Logout();
                if (string.IsNullOrWhiteSpace(user.FacultyIdImagePath))
                    await Shell.Current.Navigation.PushAsync(new Views.FacultyIdUploadPage(user.Id, user.Role));
                else
                    await Shell.Current.DisplayAlert("Employee ID review pending",
                        "Your email is verified. An authorized clinic administrator must review your Employee ID before access is activated.", "OK");
                return;
            }
            if (user.AccountStatus == "StaffIdReviewRejected" || user.AccountStatus == "FacultyReviewRejected")
            {
                auth.Logout();
                await Shell.Current.DisplayAlert("Employee ID review not approved",
                    "Contact the clinic administrator to confirm your submitted Employee ID.", "OK");
                return;
            }
            if (user.AccountStatus != "Active")
            {
                auth.Logout();
                await Shell.Current.DisplayAlert("Account unavailable", "This account is not active. Contact the clinic administrator.", "OK");
                return;
            }

            if (RememberMe)
                await Services.SecureSessionService.Instance.CreateSessionAsync(user);
            else
                await Services.SecureSessionService.Instance.ClearSessionAsync();

            await Shell.Current.Navigation.PushAsync(USJR_eCLINIC.AppShell.GetDashboard(user.Role));
        }
        catch
        {
            await Shell.Current.DisplayAlert("Login unavailable", "The local account database could not complete the login.", "OK");
        }
        finally { IsLoggingIn = false; }
    }

    private async Task ResumeRememberedAccountAsync(string identifier)
    {
        IsLoggingIn = true;
        try
        {
            var restored = await Services.SecureSessionService.Instance
                .ReauthenticateRememberedUserAsync(identifier);
            var user = Services.AuthService.Instance.CurrentUser;
            if (!restored || user == null || !user.EmailVerified || user.AccountStatus != "Active")
            {
                await Services.SecureSessionService.Instance.ClearSessionAsync();
                Services.AuthService.Instance.Logout();
                await LoadRememberedAccountAsync();
                await Shell.Current.DisplayAlert(
                    "Remembered sign-in unavailable",
                    "Please sign in using your email or ID and password.",
                    "OK");
                return;
            }

            await Shell.Current.Navigation.PushAsync(USJR_eCLINIC.AppShell.GetDashboard(user.Role));
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Sign-in unavailable",
                "The remembered account could not be opened. Use another account or sign in with your password.",
                "OK");
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private async Task UseAnotherAccount()
    {
        await Services.SecureSessionService.Instance.ClearSessionAsync();
        Services.AuthService.Instance.Logout();
        HasRememberedAccount = false;
        RememberMe = false;
        EmailOrId = string.Empty;
        Password = string.Empty;
    }

    [RelayCommand]
    private async Task SignUp() => await Shell.Current.Navigation.PushAsync(new Views.SignUpPage());

    [RelayCommand]
    private async Task ForgotPassword() => await Shell.Current.Navigation.PushAsync(new Views.ForgotPasswordPage());
}
