using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private int _userId;

    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string idLabel = "Student ID";
    [ObservableProperty] private string idNumber = string.Empty;
    [ObservableProperty] private string programOrDepartment = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string mobileNumber = string.Empty;

    [ObservableProperty] private string bloodType = string.Empty;
    [ObservableProperty] private string height = string.Empty;
    [ObservableProperty] private string weight = string.Empty;
    [ObservableProperty] private string allergies = string.Empty;
    [ObservableProperty] private string medicalConditions = string.Empty;
    [ObservableProperty] private string currentMedications = string.Empty;
    [ObservableProperty] private string emergencyContactName = string.Empty;
    [ObservableProperty] private string emergencyContactNumber = string.Empty;
    [ObservableProperty] private string emergencyContactRelationship = string.Empty;

    [ObservableProperty] private string profileImagePath = string.Empty;

    [ObservableProperty] private bool isEditing;

    public ProfileViewModel()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        _userId = user.Id;
        FullName = user.FullName;
        Role = user.Role;
        IdNumber = user.IdNumber;
        ProgramOrDepartment = user.ProgramOrDepartment;
        Email = user.Email;
        MobileNumber = user.MobileNumber;
        BloodType = user.BloodType;
        Height = user.Height;
        Weight = user.Weight;
        Allergies = user.Allergies;
        MedicalConditions = user.MedicalConditions;
        CurrentMedications = user.CurrentMedications;
        EmergencyContactName = user.EmergencyContactName;
        EmergencyContactNumber = user.EmergencyContactNumber;
        EmergencyContactRelationship = user.EmergencyContactRelationship;
        ProfileImagePath = user.ProfileImagePath;

        IdLabel = Role switch
        {
            "Student" => "Student ID",
            "Faculty" or "Admin Personnel" or "Non-Teaching" => "Employee ID",
            "R.E.A.D.S. Scholar" => "Scholar ID",
            _ => "ID Number"
        };
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
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(ProgramOrDepartment))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please fill in your name and program.", "OK");
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
            ProgramOrDepartment = ProgramOrDepartment,
            MobileNumber = MobileNumber,
            BloodType = BloodType,
            Height = Height,
            Weight = Weight,
            Allergies = Allergies,
            MedicalConditions = MedicalConditions,
            CurrentMedications = CurrentMedications,
            EmergencyContactName = EmergencyContactName,
            EmergencyContactNumber = EmergencyContactNumber,
            EmergencyContactRelationship = EmergencyContactRelationship,
            ProfileImagePath = ProfileImagePath
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

        Services.AuthService.Instance.Logout();
        await Shell.Current.Navigation.PopToRootAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}