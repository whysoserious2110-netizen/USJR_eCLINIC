using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class SignUpViewModel : ObservableObject
{
    [ObservableProperty]
    private string selectedRole = "Student";

    [ObservableProperty]
    private string idLabel = "Student ID Number";

    [ObservableProperty]
    private string idNumber = string.Empty;

    [ObservableProperty]
    private string programOrDepartment = string.Empty;

    [ObservableProperty]
    private string fullName = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool isPasswordHidden = true;

    [ObservableProperty]
    private bool isConfirmPasswordHidden = true;

    [ObservableProperty]
    private bool agreedToPrivacyPolicy;

    [ObservableProperty]
    private string passwordMatchMessage = string.Empty;

    [ObservableProperty]
    private Color passwordMatchColor = Colors.Gray;

    public SignUpViewModel()
    {
        UpdateRoleDependentFields();
    }

    [RelayCommand]
    private void SelectRole(string role)
    {
        SelectedRole = role;
        UpdateRoleDependentFields();
    }

    private void UpdateRoleDependentFields()
    {
        IdLabel = SelectedRole switch
        {
            "Student" => "Student ID Number",
            "Faculty" or "Admin Personnel" or "Non-Teaching" => "Employee ID Number",
            "R.E.A.D.S. Scholar" => "Scholar ID Number",
            _ => "ID Number"
        };
    }

    partial void OnPasswordChanged(string value) => UpdatePasswordMatchStatus();
    partial void OnConfirmPasswordChanged(string value) => UpdatePasswordMatchStatus();

    private void UpdatePasswordMatchStatus()
    {
        if (string.IsNullOrEmpty(ConfirmPassword))
        {
            PasswordMatchMessage = string.Empty;
            return;
        }

        if (Password == ConfirmPassword)
        {
            PasswordMatchMessage = "Passwords match";
            PasswordMatchColor = Color.FromArgb("#0F9B8E");
        }
        else
        {
            PasswordMatchMessage = "Passwords do not match";
            PasswordMatchColor = Color.FromArgb("#E05B5B");
        }
    }

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden;

    [RelayCommand]
    private async Task Register()
    {
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) ||
            string.IsNullOrWhiteSpace(IdNumber) || string.IsNullOrWhiteSpace(Password))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please fill in all required fields.", "OK");
            return;
        }

        if (!IsValidEmail(Email))
        {
            await Shell.Current.DisplayAlert("Invalid Email", "Please enter a valid email address (e.g. name@usjr.edu.ph or name@gmail.com).", "OK");
            return;
        }

        if (!Services.AuthService.Instance.IsAllowedEmailDomain(Email))
        {
            await Shell.Current.DisplayAlert("Email Not Allowed", "Please use a USJ-R email (@usjr.edu.ph) or a Gmail account (@gmail.com).", "OK");
            return;
        }

        if (!IsStrongPassword(Password))
        {
            await Shell.Current.DisplayAlert("Weak Password",
                "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number, and a special character.", "OK");
            return;
        }

        if (Password != ConfirmPassword)
        {
            await Shell.Current.DisplayAlert("Password mismatch", "Passwords do not match.", "OK");
            return;
        }

        if (!AgreedToPrivacyPolicy)
        {
            await Shell.Current.DisplayAlert("Privacy Policy", "Please agree to the USJ-R Health Service Data Privacy Policy.", "OK");
            return;
        }

        if (await Services.AuthService.Instance.EmailExistsAsync(Email))
        {
            await Shell.Current.DisplayAlert("Account exists", "An account with this email already exists. Please log in instead.", "OK");
            return;
        }

        if (await Services.AuthService.Instance.IdNumberExistsAsync(IdNumber))
        {
            await Shell.Current.DisplayAlert("ID already registered", "This Student/Employee ID is already registered to another account.", "OK");
            return;
        }

        var newAccount = new Models.UserAccount
        {
            FullName = FullName,
            Email = Email,
            Password = Password,
            Role = SelectedRole,
            IdNumber = IdNumber,
            ProgramOrDepartment = ProgramOrDepartment
        };

        await Services.AuthService.Instance.RegisterAsync(newAccount);

        await Shell.Current.DisplayAlert("Account Created", "Your account has been created. Please log in.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private bool IsStrongPassword(string password)
    {
        if (password.Length < 8) return false;
        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));
        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}