using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace USJR_eCLINIC.ViewModels;

public partial class SignUpViewModel : ObservableObject
{
    [ObservableProperty] private string selectedRole = "Student";
    [ObservableProperty] private string idNumber = string.Empty;
    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isPasswordHidden = true;
    [ObservableProperty] private bool isConfirmPasswordHidden = true;
    [ObservableProperty] private bool agreedToPrivacyPolicy;
    [ObservableProperty] private string passwordMatchMessage = string.Empty;
    [ObservableProperty] private Color passwordMatchColor = Colors.Gray;
    [ObservableProperty] private bool isRegistering;
    [ObservableProperty] private string allergies = string.Empty;
    [ObservableProperty] private string selectedProgramDepartment = string.Empty;
    [ObservableProperty] private string otherProgramDepartment = string.Empty;
    [ObservableProperty] private string selectedYearLevel = string.Empty;
    [ObservableProperty] private string facultyIdImagePath = string.Empty;

    public ObservableCollection<string> ProgramDepartmentOptions { get; } = new();
    public ObservableCollection<string> YearLevelOptions { get; } = new()
    {
        "1st Year", "2nd Year", "3rd Year", "4th Year", "5th Year", "6th Year"
    };
    public bool RequiresEmployeeIdReview => SelectedRole is "Faculty" or "Non-Teaching";
    public bool IsStudent => SelectedRole == "Student";
    public bool IsOtherProgramDepartment => SelectedProgramDepartment == "Other / Not listed";
    public bool HasEmployeeIdPhoto => !string.IsNullOrWhiteSpace(FacultyIdImagePath);
    public bool IsEmployeeIdPhotoMissing => !HasEmployeeIdPhoto;
    public string IdLabel => SelectedRole == "Student" ? "Student ID Number" : "Employee ID Number";
    public string IdPlaceholder => RequiresEmployeeIdReview ? "Exactly five digits" : "Enter 10-digit student ID";
    public int IdMaxLength => RequiresEmployeeIdReview ? 5 : 10;
    public Keyboard IdKeyboard => Keyboard.Numeric;
    public string IdHelperText => RequiresEmployeeIdReview
        ? "Employee ID must contain exactly five digits."
        : "Student ID must contain exactly 10 digits.";
    public string ProgramDepartmentLabel => SelectedRole switch
    {
        "Student" => "Course / School",
        "Faculty" => "School / Department",
        _ => "Office / Department"
    };
    public string ProgramDepartmentPickerTitle => SelectedRole switch
    {
        "Student" => "Select your course and school",
        "Faculty" => "Select your school or department",
        _ => "Select your office or department"
    };
    public string EmployeeIdPhotoButtonText => HasEmployeeIdPhoto
        ? "Replace Employee ID photo" : "Upload Employee ID photo";
    public string EmployeeIdPhotoName => HasEmployeeIdPhoto
        ? Path.GetFileName(FacultyIdImagePath) : string.Empty;
    public string PasswordVisibilityText => IsPasswordHidden ? "SHOW" : "HIDE";
    public string ConfirmPasswordVisibilityText => IsConfirmPasswordHidden ? "SHOW" : "HIDE";

    partial void OnSelectedRoleChanged(string value)
    {
        IdNumber = string.Empty;
        SelectedProgramDepartment = string.Empty;
        OtherProgramDepartment = string.Empty;
        SelectedYearLevel = string.Empty;
        if (value == "Student")
            FacultyIdImagePath = string.Empty;
        OnPropertyChanged(nameof(RequiresEmployeeIdReview));
        OnPropertyChanged(nameof(IsStudent));
        OnPropertyChanged(nameof(IsOtherProgramDepartment));
        OnPropertyChanged(nameof(IdLabel));
        OnPropertyChanged(nameof(IdPlaceholder));
        OnPropertyChanged(nameof(IdMaxLength));
        OnPropertyChanged(nameof(IdKeyboard));
        OnPropertyChanged(nameof(IdHelperText));
        OnPropertyChanged(nameof(ProgramDepartmentLabel));
        OnPropertyChanged(nameof(ProgramDepartmentPickerTitle));
        SetProgramDepartmentOptions();
    }

    partial void OnSelectedProgramDepartmentChanged(string value) => OnPropertyChanged(nameof(IsOtherProgramDepartment));

    partial void OnIdNumberChanged(string value)
    {
        var digitsOnly = new string((value ?? string.Empty)
            .Where(char.IsAsciiDigit)
            .Take(IdMaxLength)
            .ToArray());

        if (!string.Equals(value, digitsOnly, StringComparison.Ordinal))
            IdNumber = digitsOnly;
    }

    partial void OnFacultyIdImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasEmployeeIdPhoto));
        OnPropertyChanged(nameof(IsEmployeeIdPhotoMissing));
        OnPropertyChanged(nameof(EmployeeIdPhotoButtonText));
        OnPropertyChanged(nameof(EmployeeIdPhotoName));
    }
    partial void OnIsPasswordHiddenChanged(bool value) => OnPropertyChanged(nameof(PasswordVisibilityText));
    partial void OnIsConfirmPasswordHiddenChanged(bool value) => OnPropertyChanged(nameof(ConfirmPasswordVisibilityText));
    partial void OnPasswordChanged(string value) => UpdatePasswordMatchStatus();
    partial void OnConfirmPasswordChanged(string value) => UpdatePasswordMatchStatus();

    public SignUpViewModel() => SetProgramDepartmentOptions();

    private static readonly string[] StudentPrograms =
    {
        "BS Accountancy - School of Business & Management",
        "BS Business Administration - School of Business & Management",
        "BS Entrepreneurship - School of Business & Management",
        "BS Hospitality Management - School of Business & Management",
        "BS Tourism Management - School of Business & Management",
        "BSCPE - School of Engineering and Architecture",
        "BS Civil Engineering - School of Engineering and Architecture",
        "BS Architecture - School of Engineering and Architecture",
        "BS Computer Science - School of Computer Studies",
        "BS Information Systems - School of Computer Studies",
        "BS Information Technology - School of Computer Studies",
        "BS Psychology - School of Arts & Sciences",
        "BS Nursing - School of Allied Medical Sciences",
        "BS Medical Laboratory Science - School of Allied Medical Sciences",
        "Education - School of Education",
        "Other / Not listed"
    };

    private static readonly string[] FacultyDepartments =
    {
        "School of Arts & Sciences",
        "School of Business & Management",
        "School of Computer Studies",
        "School of Education",
        "School of Engineering and Architecture",
        "School of Allied Medical Sciences",
        "School of Law",
        "Graduate School",
        "Other / Not listed"
    };

    private static readonly string[] NonTeachingDepartments =
    {
        "Human Resources Office",
        "Registrar's Office",
        "Finance Office",
        "Office of Student Affairs and Services",
        "Guidance Office",
        "Library",
        "Information Technology Office",
        "Campus Ministry",
        "General Services / Maintenance",
        "Security Office",
        "Other / Not listed"
    };

    private void SetProgramDepartmentOptions()
    {
        ProgramDepartmentOptions.Clear();
        var source = SelectedRole switch
        {
            "Student" => StudentPrograms,
            "Faculty" => FacultyDepartments,
            _ => NonTeachingDepartments
        };
        foreach (var option in source)
            ProgramDepartmentOptions.Add(option);
    }

    private void UpdatePasswordMatchStatus()
    {
        if (string.IsNullOrEmpty(ConfirmPassword))
        {
            PasswordMatchMessage = string.Empty;
            PasswordMatchColor = Colors.Gray;
        }
        else if (Password == ConfirmPassword)
        {
            PasswordMatchMessage = "Passwords match";
            PasswordMatchColor = Color.FromArgb("#087A3D");
        }
        else
        {
            PasswordMatchMessage = "Passwords do not match";
            PasswordMatchColor = Color.FromArgb("#C0392B");
        }
    }

    [RelayCommand]
    private async Task SelectRole(string role)
    {
        if (role is not ("Student" or "Faculty" or "Non-Teaching")) return;
        SelectedRole = role;
    }

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordHidden = !IsPasswordHidden;

    [RelayCommand]
    private void ToggleConfirmPasswordVisibility() => IsConfirmPasswordHidden = !IsConfirmPasswordHidden;

    [RelayCommand]
    private async Task ChooseEmployeeIdImage()
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose your Employee ID photo",
                FileTypes = FilePickerFileType.Images
            });
            if (file == null) return;

            var extension = Path.GetExtension(file.FileName);
            var destination = Path.Combine(FileSystem.AppDataDirectory,
                $"faculty_id_{Guid.NewGuid():N}{extension}");
            await using var source = await file.OpenReadAsync();
            await using var output = File.Create(destination);
            await source.CopyToAsync(output);
            FacultyIdImagePath = destination;
        }
        catch
        {
            await Shell.Current.DisplayAlert("Photo unavailable", "The selected ID image could not be saved on this device.", "OK");
        }
    }

    [RelayCommand]
    private async Task Register()
    {
        if (IsRegistering) return;
        var name = FullName?.Trim() ?? string.Empty;
        var id = IdNumber?.Trim() ?? string.Empty;
        var normalizedEmail = Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var enteredPassword = Password ?? string.Empty;
        var selectedProgramDepartment = SelectedProgramDepartment?.Trim() ?? string.Empty;
        var selectedProgramOrDepartment = selectedProgramDepartment == "Other / Not listed"
            ? OtherProgramDepartment?.Trim() ?? string.Empty
            : selectedProgramDepartment;
        var studentProgramAndYear = string.Empty;
        if (IsStudent && selectedProgramDepartment == "Other / Not listed" &&
            !string.IsNullOrWhiteSpace(selectedProgramOrDepartment))
        {
            studentProgramAndYear = $"{selectedProgramOrDepartment} - {SelectedYearLevel}";
        }
        else if (IsStudent && !string.IsNullOrWhiteSpace(selectedProgramDepartment))
        {
            var separatorIndex = selectedProgramDepartment.IndexOf(" - ", StringComparison.Ordinal);
            var course = separatorIndex >= 0
                ? selectedProgramDepartment[..separatorIndex]
                : selectedProgramDepartment;
            var school = separatorIndex >= 0
                ? selectedProgramDepartment[(separatorIndex + 3)..]
                : string.Empty;
            studentProgramAndYear = $"{course} - {SelectedYearLevel}";
            selectedProgramOrDepartment = string.IsNullOrWhiteSpace(school)
                ? studentProgramAndYear
                : $"{studentProgramAndYear}, {school}";
        }

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(selectedProgramOrDepartment) ||
            string.IsNullOrEmpty(enteredPassword))
        {
            await Shell.Current.DisplayAlert("Missing information", "Complete all required fields.", "OK");
            return;
        }
        if (IsStudent && string.IsNullOrWhiteSpace(SelectedYearLevel))
        {
            await Shell.Current.DisplayAlert("Year level required", "Select your current year level.", "OK");
            return;
        }
        if (IsStudent && !Regex.IsMatch(id, "^[0-9]{10}$"))
        {
            await Shell.Current.DisplayAlert("Invalid Student ID", "Student ID must contain exactly 10 digits.", "OK");
            return;
        }
        if (RequiresEmployeeIdReview && !Regex.IsMatch(id, "^\\d{5}$"))
        {
            await Shell.Current.DisplayAlert("Invalid Employee ID", "Faculty and Non-Teaching employee IDs must contain exactly five digits.", "OK");
            return;
        }
        if (!IsValidEmail(normalizedEmail) || !normalizedEmail.EndsWith("@usjr.edu.ph", StringComparison.OrdinalIgnoreCase))
        {
            await Shell.Current.DisplayAlert("USJ-R email required", "Enter a valid @usjr.edu.ph email address.", "OK");
            return;
        }
        if (RequiresEmployeeIdReview && string.IsNullOrWhiteSpace(FacultyIdImagePath))
        {
            await Shell.Current.DisplayAlert("Employee ID photo required", "Upload your university Employee ID photo for an authorized review.", "OK");
            return;
        }
        if (!IsStrongPassword(enteredPassword))
        {
            await Shell.Current.DisplayAlert("Weak password", "Use at least 8 characters with uppercase, lowercase, number, and special character.", "OK");
            return;
        }
        if (!string.Equals(enteredPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            await Shell.Current.DisplayAlert("Password mismatch", "The passwords do not match.", "OK");
            return;
        }
        if (!AgreedToPrivacyPolicy)
        {
            await Shell.Current.DisplayAlert("Privacy policy", "Please agree to the USJ-R Health Service Data Privacy Policy.", "OK");
            return;
        }

        IsRegistering = true;
        try
        {
            var auth = Services.AuthService.Instance;
            if (await auth.IdNumberExistsAsync(id) || await auth.EmailExistsAsync(normalizedEmail))
            {
                await Shell.Current.DisplayAlert("Already registered", "That ID number or email is already registered.", "OK");
                return;
            }

            var account = new Models.UserAccount
            {
                FullName = name,
                Email = normalizedEmail,
                Password = enteredPassword,
                Role = SelectedRole,
                IdNumber = id,
                ProgramOrDepartment = selectedProgramOrDepartment,
                StudentProgramAndYear = studentProgramAndYear,
                Allergies = Allergies.Trim(),
                FacultyIdImagePath = RequiresEmployeeIdReview ? FacultyIdImagePath : string.Empty,
                EmailVerified = false,
                AccountStatus = "PendingEmailVerification"
            };
            await auth.RegisterAsync(account);
            var send = await Services.EmailVerificationService.Instance.StartVerificationAsync(account.Id);
            auth.Logout();
            Password = string.Empty;
            ConfirmPassword = string.Empty;
            await Shell.Current.Navigation.PopAsync();
            await Shell.Current.Navigation.PushAsync(new Views.EmailVerificationPage(account.Id, send.Message));
        }
        catch
        {
            await Shell.Current.DisplayAlert("Registration unavailable", "The account could not be saved locally. Please try again.", "OK");
        }
        finally { IsRegistering = false; }
    }

    private static bool IsValidEmail(string value)
    {
        try { return new System.Net.Mail.MailAddress(value).Address.Equals(value, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }

    private static bool IsStrongPassword(string value) => value.Length >= 8 && value.Any(char.IsUpper) &&
        value.Any(char.IsLower) && value.Any(char.IsDigit) && value.Any(c => !char.IsLetterOrDigit(c));

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
