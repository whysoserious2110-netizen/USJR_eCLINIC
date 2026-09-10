using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class AdminPatientViewModel : ObservableObject
{
    private string _patientEmail = string.Empty;

    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private string idNumber = string.Empty;
    [ObservableProperty] private string programOrDepartment = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string mobileNumber = string.Empty;
    [ObservableProperty] private string emergencyContactName = string.Empty;
    [ObservableProperty] private string emergencyContactNumber = string.Empty;
    [ObservableProperty] private string emergencyContactRelationship = string.Empty;
    [ObservableProperty] private string profileImagePath = string.Empty;

    public AdminPatientViewModel(string patientEmail)
    {
        _patientEmail = patientEmail;
        _ = LoadAsync(patientEmail);
    }

    private async Task LoadAsync(string patientEmail)
    {
        var account = await Services.AuthService.Instance.GetAccountByEmailAsync(patientEmail);
        if (account == null) return;

        FullName = account.FullName;
        Role = account.Role;
        IdNumber = account.IdNumber;
        ProgramOrDepartment = account.ProgramOrDepartment;
        Email = account.Email;
        MobileNumber = account.MobileNumber;
        EmergencyContactName = account.EmergencyContactName;
        EmergencyContactNumber = account.EmergencyContactNumber;
        EmergencyContactRelationship = account.EmergencyContactRelationship;
        ProfileImagePath = account.ProfileImagePath;
    }

    [RelayCommand]
    private async Task GoToNotes()
        => await Shell.Current.Navigation.PushAsync(new Views.AdminNotesPage(_patientEmail, FullName));

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}