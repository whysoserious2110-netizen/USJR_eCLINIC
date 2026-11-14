using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class EmailVerificationViewModel : ObservableObject
{
    private readonly int _userId;

    [ObservableProperty] private string verificationCode = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool hasStatusMessage;
    [ObservableProperty] private bool isBusy;

    public EmailVerificationViewModel(int userId, string initialMessage)
    {
        _userId = userId;
        SetStatus(initialMessage);
    }

    [RelayCommand]
    private async Task Verify()
    {
        if (IsBusy) return;
        var code = VerificationCode?.Trim() ?? string.Empty;
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            SetStatus("Enter the six-digit code sent to your email.");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await Services.EmailVerificationService.Instance.VerifyAsync(_userId, code);
            SetStatus(result.Message);
            if (!result.Success) return;

            await Shell.Current.DisplayAlert("Email verified", result.Message, "Continue");
            await Shell.Current.Navigation.PopAsync();
        }
        catch
        {
            SetStatus("Verification could not be completed. Try again.");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Resend()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await Services.EmailVerificationService.Instance.StartVerificationAsync(_userId);
            SetStatus(result.Success ? result.Message : result.Message +
                (result.RetryAfterSeconds > 0 ? $" Try again in {result.RetryAfterSeconds} seconds." : string.Empty));
            VerificationCode = string.Empty;
        }
        catch { SetStatus("The verification email could not be sent. Check the local Email API."); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BackToLogin() => await Shell.Current.Navigation.PopAsync();

    private void SetStatus(string message)
    {
        StatusMessage = message;
        HasStatusMessage = !string.IsNullOrWhiteSpace(message);
    }
}
