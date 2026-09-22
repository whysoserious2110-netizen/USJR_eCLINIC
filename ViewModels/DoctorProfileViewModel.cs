using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class DoctorProfileViewModel : ObservableObject
{
    private int _userId;

    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string department = string.Empty;
    [ObservableProperty] private string specialization = string.Empty;
    [ObservableProperty] private string licenseNumber = string.Empty;
    [ObservableProperty] private string yearsOfExperience = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string mobileNumber = string.Empty;
    [ObservableProperty] private string consultationSchedule = string.Empty;
    [ObservableProperty] private string profileImagePath = string.Empty;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private string position = string.Empty;
    [ObservableProperty] private string employmentStatus = string.Empty;

    public DoctorProfileViewModel()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        _userId = user.Id;
        FullName = user.FullName;
        Department = user.ProgramOrDepartment;
        Specialization = user.Specialization;
        LicenseNumber = user.LicenseNumber;
        YearsOfExperience = user.YearsOfExperience;
        Email = user.Email;
        MobileNumber = user.MobileNumber;
        ConsultationSchedule = user.ConsultationSchedule;
        ProfileImagePath = user.ProfileImagePath;
        Position = user.Position;
        EmploymentStatus = user.EmploymentStatus;
    }

    [RelayCommand]
    private void ToggleEdit() => IsEditing = !IsEditing;

    [RelayCommand]
    private async Task ChangePhoto()
    {
        string action = await Shell.Current.DisplayActionSheet("Change Photo", "Cancel", null, "Take Photo", "Choose from Gallery");

        FileResult? result = null;

        try
        {
            if (action == "Take Photo")
            {
                if (MediaPicker.Default.IsCaptureSupported)
                    result = await MediaPicker.Default.CapturePhotoAsync();
            }
            else if (action == "Choose from Gallery")
            {
                result = await MediaPicker.Default.PickPhotoAsync();
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", $"Could not access camera/gallery: {ex.Message}", "OK");
            return;
        }

        if (result == null) return;

        var localPath = Path.Combine(FileSystem.AppDataDirectory, $"profile_{_userId}.jpg");

        using (var sourceStream = await result.OpenReadAsync())
        using (var localStream = File.Create(localPath))
        {
            await sourceStream.CopyToAsync(localStream);
        }

        ProfileImagePath = localPath;

        var current = Services.AuthService.Instance.CurrentUser!;
        current.ProfileImagePath = localPath;
        await Services.AuthService.Instance.UpdateProfileAsync(current);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Specialization))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please fill in your name and specialization.", "OK");
            return;
        }

        var current = Services.AuthService.Instance.CurrentUser!;

        var updated = new Models.UserAccount
        {
            Id = _userId,
            FullName = FullName,
            Email = current.Email,
            Password = current.Password,
            Role = current.Role,
            IdNumber = current.IdNumber,
            ProgramOrDepartment = Department,
            Specialization = Specialization,
            LicenseNumber = LicenseNumber,
            YearsOfExperience = YearsOfExperience,
            MobileNumber = MobileNumber,
            ConsultationSchedule = ConsultationSchedule,
            ProfileImagePath = ProfileImagePath,
            Position = Position,
            EmploymentStatus = EmploymentStatus
        };

        var success = await Services.AuthService.Instance.UpdateProfileAsync(updated);

        if (success)
        {
            IsEditing = false;
            await Shell.Current.DisplayAlert("Profile Updated", "Your profile has been updated.", "OK");
        }
        else
        {
            await Shell.Current.DisplayAlert("Error", "Could not update profile. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        bool confirm = await Shell.Current.DisplayAlert("Log Out", "Are you sure you want to log out?", "Log Out", "Cancel");
        if (!confirm) return;

        await Services.SecureSessionService.Instance
    .ClearSessionAsync();

        Services.AuthService.Instance.Logout();

        await Shell.Current.Navigation.PopToRootAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}