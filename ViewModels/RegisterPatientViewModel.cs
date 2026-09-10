using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class RegisterPatientViewModel : ObservableObject
{
    public ObservableCollection<string> Roles { get; } = new()
    {
        "Student", "Faculty", "Admin Personnel", "Non-Teaching", "R.E.A.D.S. Scholar"
    };

    [ObservableProperty] private string selectedRole = "Student";
    [ObservableProperty] private string idNumber = string.Empty;
    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string programOrDepartment = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string mobileNumber = string.Empty;

    [RelayCommand]
    private async Task Register()
    {
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(IdNumber) || string.IsNullOrWhiteSpace(Email))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please fill in Full Name, ID Number, and Email.", "OK");
            return;
        }

        if (await Services.AuthService.Instance.IdNumberExistsAsync(IdNumber))
        {
            await Shell.Current.DisplayAlert("Already registered", "This ID number is already registered.", "OK");
            return;
        }

        if (await Services.AuthService.Instance.EmailExistsAsync(Email))
        {
            await Shell.Current.DisplayAlert("Already registered", "This email is already registered.", "OK");
            return;
        }

        const string tempPassword = "Welcome123!";

        var account = new Models.UserAccount
        {
            FullName = FullName,
            Email = Email,
            Password = tempPassword,
            Role = SelectedRole,
            IdNumber = IdNumber,
            ProgramOrDepartment = ProgramOrDepartment,
            MobileNumber = MobileNumber
        };

        await Services.AuthService.Instance.RegisterAsync(account);

        await Shell.Current.DisplayAlert("Registered",
            $"{FullName} has been registered.\n\nTemporary password: {tempPassword}\n\nPlease advise the patient to log in and update their profile.", "OK");

        FullName = string.Empty;
        IdNumber = string.Empty;
        ProgramOrDepartment = string.Empty;
        Email = string.Empty;
        MobileNumber = string.Empty;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}