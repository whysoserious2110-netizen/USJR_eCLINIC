using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;

namespace USJR_eCLINIC.ViewModels;

public partial class SignUpViewModel :
    ObservableObject
{
    [ObservableProperty]
    private string selectedRole = "Student";

    [ObservableProperty]
    private string idLabel =
        "Student ID Number";

    [ObservableProperty]
    private string idNumber = string.Empty;

    [ObservableProperty]
    private string programOrDepartment =
        string.Empty;

    [ObservableProperty]
    private string fullName = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword =
        string.Empty;

    [ObservableProperty]
    private bool isPasswordHidden = true;

    [ObservableProperty]
    private bool isConfirmPasswordHidden = true;

    [ObservableProperty]
    private bool agreedToPrivacyPolicy;

    [ObservableProperty]
    private string passwordMatchMessage =
        string.Empty;

    [ObservableProperty]
    private Color passwordMatchColor =
        Colors.Gray;

    [ObservableProperty]
    private bool isRegistering;

    public SignUpViewModel()
    {
        SelectedRole = "Student";
        IdLabel = "Student ID Number";
    }

    private static readonly string[] SelfRegistrationRoles =
    {
        "Student",
        "Faculty",
        "Admin Personnel",
        "Non-Teaching"
    };

    [RelayCommand]
    private async Task SelectRole(
        string role)
    {
        if (!SelfRegistrationRoles.Contains(role))
        {
            await Shell.Current.DisplayAlert(
                "Staff Registration",
                "Doctor, Nurse, Dentist, and R.E.A.D.S. " +
                "Scholar accounts must be created by " +
                "authorized personnel.",
                "OK");

            return;
        }

        SelectedRole = role;

        IdLabel =
            role == "Student"
                ? "Student ID Number"
                : "Employee ID Number";
    }

    partial void OnPasswordChanged(
        string value)
    {
        UpdatePasswordMatchStatus();
    }

    partial void OnConfirmPasswordChanged(
        string value)
    {
        UpdatePasswordMatchStatus();
    }

    private void UpdatePasswordMatchStatus()
    {
        if (string.IsNullOrEmpty(
            ConfirmPassword))
        {
            PasswordMatchMessage =
                string.Empty;

            PasswordMatchColor =
                Colors.Gray;

            return;
        }

        if (Password == ConfirmPassword)
        {
            PasswordMatchMessage =
                "Passwords match";

            PasswordMatchColor =
                Color.FromArgb("#0F9B8E");
        }
        else
        {
            PasswordMatchMessage =
                "Passwords do not match";

            PasswordMatchColor =
                Color.FromArgb("#E05B5B");
        }
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden =
            !IsPasswordHidden;
    }

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility()
    {
        IsConfirmPasswordHidden =
            !IsConfirmPasswordHidden;
    }

    [RelayCommand]
    private async Task Register()
    {
        if (IsRegistering)
            return;

        var normalizedName =
            FullName?.Trim() ??
            string.Empty;

        var normalizedStudentId =
            IdNumber?.Trim()
                .ToUpperInvariant() ??
            string.Empty;

        var normalizedEmail =
            Email?.Trim()
                .ToLowerInvariant() ??
            string.Empty;

        var enteredPassword =
            Password ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                normalizedName) ||
            string.IsNullOrWhiteSpace(
                normalizedStudentId) ||
            string.IsNullOrWhiteSpace(
                normalizedEmail) ||
            string.IsNullOrEmpty(
                enteredPassword))
        {
            await Shell.Current.DisplayAlert(
                "Missing information",
                "Please fill in all required fields.",
                "OK");

            return;
        }

        if (!IsValidEmail(normalizedEmail))
        {
            await Shell.Current.DisplayAlert(
                "Invalid Email",
                "Enter a valid USJ-R email address.",
                "OK");

            return;
        }

        if (!normalizedEmail.EndsWith(
            "@usjr.edu.ph",
            StringComparison.OrdinalIgnoreCase))
        {
            await Shell.Current.DisplayAlert(
                "USJ-R Email Required",
                "Registration requires an official " +
                "@usjr.edu.ph email address.",
                "OK");

            return;
        }

        if (!IsStrongPassword(
            enteredPassword))
        {
            await Shell.Current.DisplayAlert(
                "Weak Password",
                "Password must be at least 8 characters " +
                "and include uppercase, lowercase, number, " +
                "and special characters.",
                "OK");

            return;
        }

        if (!string.Equals(
            enteredPassword,
            ConfirmPassword,
            StringComparison.Ordinal))
        {
            await Shell.Current.DisplayAlert(
                "Password mismatch",
                "Passwords do not match.",
                "OK");

            return;
        }

        if (!AgreedToPrivacyPolicy)
        {
            await Shell.Current.DisplayAlert(
                "Privacy Policy",
                "Please agree to the USJ-R Health " +
                "Service Data Privacy Policy.",
                "OK");

            return;
        }

        try
        {
            IsRegistering = true;

            if (await Services.AuthService.Instance
                    .IdNumberExistsAsync(normalizedStudentId) ||
                await Services.AuthService.Instance
                    .EmailExistsAsync(normalizedEmail))
            {
                await Shell.Current.DisplayAlert(
                    "Already registered",
                    "That ID number or email address is already registered.",
                    "OK");

                return;
            }

            var account = new Models.UserAccount
            {
                FullName = normalizedName,
                Email = normalizedEmail,
                Password = enteredPassword,
                Role = SelectedRole,
                IdNumber = normalizedStudentId,
                ProgramOrDepartment =
                    ProgramOrDepartment?.Trim() ?? string.Empty
            };

            await Services.AuthService.Instance
                .RegisterAsync(account);

            Services.AuthService.Instance.Logout();

            Password = string.Empty;
            ConfirmPassword = string.Empty;

            await Shell.Current.DisplayAlert(
                "Account Created",
                "Your local account has been " +
                "created. Please log in.",
                "Continue");

            await Shell.Current.Navigation
                .PopAsync();
        }
        catch
        {
            await Shell.Current.DisplayAlert(
                "Registration Unavailable",
                "The local account database could not complete " +
                "the registration. Please try again.",
                "OK");
        }
        finally
        {
            IsRegistering = false;
        }
    }

    private static bool IsValidEmail(
        string email)
    {
        try
        {
            var address =
                new System.Net.Mail.MailAddress(
                    email);

            return string.Equals(
                address.Address,
                email,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStrongPassword(
        string password)
    {
        if (password.Length < 8)
            return false;

        return password.Any(char.IsUpper) &&
               password.Any(char.IsLower) &&
               password.Any(char.IsDigit) &&
               password.Any(character =>
                   !char.IsLetterOrDigit(character));
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation
            .PopAsync();
    }
}