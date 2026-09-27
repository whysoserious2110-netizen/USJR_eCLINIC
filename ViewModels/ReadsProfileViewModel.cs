using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ReadsProfileViewModel : ObservableObject
{
    private readonly int _userId;

    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string role = "R.E.A.D.S. Scholar";
    [ObservableProperty] private string studentId = string.Empty;
    [ObservableProperty] private string programAndYear = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string mobileNumber = string.Empty;
    [ObservableProperty] private string profileImagePath = string.Empty;
    [ObservableProperty] private bool isEditing;

    public ReadsProfileViewModel()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        _userId = user.Id;
        FullName = user.FullName;
        Role = user.Role;
        StudentId = user.IdNumber;
        ProgramAndYear = user.ProgramOrDepartment;
        Email = user.Email;
        MobileNumber = user.MobileNumber;
        ProfileImagePath = user.ProfileImagePath;
    }

    [RelayCommand]
    private void ToggleEdit() => IsEditing = !IsEditing;

    [RelayCommand]
    private async Task ChangePhoto()
    {
        string action = await Shell.Current.DisplayActionSheet(
            "Change Photo",
            "Cancel",
            null,
            "Take Photo",
            "Choose from Gallery");

        FileResult? result = null;

        try
        {
            if (action == "Take Photo" && MediaPicker.Default.IsCaptureSupported)
                result = await MediaPicker.Default.CapturePhotoAsync();
            else if (action == "Choose from Gallery")
                result = await MediaPicker.Default.PickPhotoAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert(
                "Error",
                $"Could not access camera/gallery: {ex.Message}",
                "OK");
            return;
        }

        if (result == null) return;

        string localPath = Path.Combine(
            FileSystem.AppDataDirectory,
            $"profile_{_userId}.jpg");

        using (var sourceStream = await result.OpenReadAsync())
        using (var localStream = File.Create(localPath))
        {
            await sourceStream.CopyToAsync(localStream);
        }

        ProfileImagePath = localPath;

        var current = Services.AuthService.Instance.CurrentUser;
        if (current == null) return;

        current.ProfileImagePath = localPath;
        await Services.AuthService.Instance.UpdateProfileAsync(current);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(FullName) ||
            string.IsNullOrWhiteSpace(ProgramAndYear))
        {
            await Shell.Current.DisplayAlert(
                "Missing info",
                "Please fill in your name and program/year.",
                "OK");
            return;
        }

        var current = Services.AuthService.Instance.CurrentUser;
        if (current == null)
        {
            await Shell.Current.DisplayAlert(
                "Not logged in",
                "Please log in again.",
                "OK");
            return;
        }

        // Keep the account's other information unchanged.
        var updated = new Models.UserAccount
        {
            Id = _userId,
            FullName = FullName,
            Email = current.Email,
            Password = current.Password,
            Role = current.Role,
            IdNumber = StudentId,
            ProgramOrDepartment = ProgramAndYear,
            MobileNumber = MobileNumber,

            BloodType = current.BloodType,
            Height = current.Height,
            Weight = current.Weight,
            Allergies = current.Allergies,
            MedicalConditions = current.MedicalConditions,
            CurrentMedications = current.CurrentMedications,
            EmergencyContactName = current.EmergencyContactName,
            EmergencyContactNumber = current.EmergencyContactNumber,
            EmergencyContactRelationship = current.EmergencyContactRelationship,

            ProfileImagePath = ProfileImagePath,
            Specialization = current.Specialization,
            LicenseNumber = current.LicenseNumber,
            YearsOfExperience = current.YearsOfExperience,
            ConsultationSchedule = current.ConsultationSchedule,
            Position = current.Position,
            EmploymentStatus = current.EmploymentStatus,
            LicenseExpiration = current.LicenseExpiration
        };

        bool success =
            await Services.AuthService.Instance.UpdateProfileAsync(updated);

        if (success)
        {
            IsEditing = false;
            await Shell.Current.DisplayAlert(
                "Profile Updated",
                "Your profile has been updated.",
                "OK");
        }
        else
        {
            await Shell.Current.DisplayAlert(
                "Error",
                "Could not update profile. Please try again.",
                "OK");
        }
    }

    [RelayCommand]
    private async Task Logout()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Log Out",
            "Are you sure you want to log out?",
            "Log Out",
            "Cancel");

        if (!confirm) return;

        await Services.SecureSessionService.Instance.ClearSessionAsync();
        Services.AuthService.Instance.Logout();
        await Shell.Current.Navigation.PopToRootAsync();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}