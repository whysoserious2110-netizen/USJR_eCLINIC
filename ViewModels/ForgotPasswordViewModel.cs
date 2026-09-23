using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ForgotPasswordViewModel : ObservableObject
{
    private string _resetToken = string.Empty;

    [ObservableProperty]
    private int currentStep = 1;

    [ObservableProperty]
    private string studentId = string.Empty;

    [ObservableProperty]
    private string verificationCode = string.Empty;

    [ObservableProperty]
    private string newPassword = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private string recoveryText = string.Empty;

    [ObservableProperty]
    private string demoCode = string.Empty;

    [ObservableProperty]
    private bool hasDemoCode;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool hasStatusMessage;

    [ObservableProperty]
    private bool isBusy;

    public bool IsStudentIdStep => CurrentStep == 1;
    public bool IsVerificationStep => CurrentStep == 2;
    public bool IsNewPasswordStep => CurrentStep == 3;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStudentIdStep));
        OnPropertyChanged(nameof(IsVerificationStep));
        OnPropertyChanged(nameof(IsNewPasswordStep));
    }

    [RelayCommand]
    private async Task SendCode()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(StudentId))
        {
            ShowStatus("Enter your Student or Employee ID.");
            return;
        }

        try
        {
            IsBusy = true;
            ClearStatus();

            StudentId =
                StudentId.Trim()
                    .ToUpperInvariant();

            var result =
                await Services.PasswordResetService.Instance
                    .StartResetAsync(StudentId);

            if (!result.AccountFound)
            {
                ShowStatus(
                    "No local account was found for that ID. " +
                    "Check the ID and try again.");

                return;
            }

            DemoCode = result.DemoCode;

            HasDemoCode =
                !string.IsNullOrWhiteSpace(
                    DemoCode);

            RecoveryText =
                $"Use the demo verification code for " +
                $"{result.RecoveryDisplay}.";

            VerificationCode =
                string.Empty;

            CurrentStep = 2;
        }
        catch
        {
            ShowStatus(
                "The local password-reset request " +
                "could not be completed.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task VerifyCode()
    {
        if (IsBusy)
            return;

        var code =
            VerificationCode?.Trim() ??
            string.Empty;

        if (code.Length != 6 ||
            !code.All(char.IsDigit))
        {
            ShowStatus(
                "Enter the six-digit demo code.");

            return;
        }

        try
        {
            IsBusy = true;
            ClearStatus();

            var result =
                await Services.PasswordResetService.Instance
                    .VerifyCodeAsync(
                        StudentId,
                        code);

            if (!result.Success ||
                string.IsNullOrWhiteSpace(
                    result.ResetToken))
            {
                ShowStatus(
                    string.IsNullOrWhiteSpace(
                        result.Message)
                        ? "The verification code is " +
                          "invalid or expired."
                        : result.Message);

                return;
            }

            _resetToken =
                result.ResetToken;

            NewPassword =
                string.Empty;

            ConfirmPassword =
                string.Empty;

            CurrentStep = 3;
        }
        catch
        {
            ShowStatus(
                "The demo code could not be verified.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResendCode()
    {
        if (IsBusy)
            return;

        VerificationCode =
            string.Empty;

        DemoCode =
            string.Empty;

        HasDemoCode =
            false;

        ClearStatus();

        await SendCode();
    }

    [RelayCommand]
    private async Task ResetPassword()
    {
        if (IsBusy)
            return;

        if (string.IsNullOrWhiteSpace(
            NewPassword))
        {
            ShowStatus(
                "Enter your new password.");

            return;
        }

        if (!string.Equals(
                NewPassword,
                ConfirmPassword,
                StringComparison.Ordinal))
        {
            ShowStatus(
                "The passwords do not match.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
            _resetToken))
        {
            ShowStatus(
                "The reset session is invalid. " +
                "Request a new code.");

            return;
        }

        try
        {
            IsBusy = true;
            ClearStatus();

            var result =
                await Services.PasswordResetService.Instance
                    .ResetPasswordAsync(
                        _resetToken,
                        NewPassword);

            if (!result.Success)
            {
                ShowStatus(
                    string.IsNullOrWhiteSpace(
                        result.Message)
                        ? "The password could not be reset."
                        : result.Message);

                return;
            }

            await Services.SecureSessionService.Instance
                .ClearSessionAsync();

            Services.AuthService.Instance.Logout();

            await Shell.Current.DisplayAlert(
                "Password Reset Successful",
                "Your password has been changed. " +
                "Sign in using your new password.",
                "Continue");

            StudentId =
                string.Empty;

            VerificationCode =
                string.Empty;

            NewPassword =
                string.Empty;

            ConfirmPassword =
                string.Empty;

            RecoveryText =
                string.Empty;

            DemoCode =
                string.Empty;

            HasDemoCode =
                false;

            _resetToken =
                string.Empty;

            CurrentStep = 1;

            await Shell.Current.Navigation
                .PopAsync();
        }
        catch
        {
            ShowStatus(
                "Your password could not be changed. " +
                "Please try again.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ChangeStudentId()
    {
        CurrentStep = 1;

        VerificationCode =
            string.Empty;

        RecoveryText =
            string.Empty;

        DemoCode =
            string.Empty;

        HasDemoCode =
            false;

        _resetToken =
            string.Empty;

        ClearStatus();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        if (CurrentStep == 3)
        {
            CurrentStep = 2;

            NewPassword =
                string.Empty;

            ConfirmPassword =
                string.Empty;

            _resetToken =
                string.Empty;

            ClearStatus();

            return;
        }

        if (CurrentStep == 2)
        {
            ChangeStudentId();
            return;
        }

        await Shell.Current.Navigation
            .PopAsync();
    }

    private void ShowStatus(
        string message)
    {
        StatusMessage =
            message;

        HasStatusMessage =
            true;
    }

    private void ClearStatus()
    {
        StatusMessage =
            string.Empty;

        HasStatusMessage =
            false;
    }
}