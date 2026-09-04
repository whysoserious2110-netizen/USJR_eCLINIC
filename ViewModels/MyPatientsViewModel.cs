using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class MyPatientItem : ObservableObject
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string LastVisitDisplay { get; set; } = string.Empty;
    public string ProfileImagePath { get; set; } = string.Empty;
}

public partial class MyPatientsViewModel : ObservableObject
{
    public ObservableCollection<MyPatientItem> Patients { get; } = new();

    [ObservableProperty]
    private bool hasPatients;

    public async Task RefreshAsync()
    {
        var doctor = Services.AuthService.Instance.CurrentUser;
        if (doctor == null) return;

        var emails = await Services.MedicalRecordService.Instance.GetDistinctPatientEmailsForStaffAsync(doctor.FullName);

        Patients.Clear();

        foreach (var email in emails)
        {
            var account = await Services.AuthService.Instance.GetAccountByEmailAsync(email);
            var lastVisit = await Services.MedicalRecordService.Instance.GetLastVisitDateAsync(email);

            Patients.Add(new MyPatientItem
            {
                Email = email,
                FullName = account?.FullName ?? email,
                Role = account?.Role ?? string.Empty,
                IdNumber = account?.IdNumber ?? string.Empty,
                ProfileImagePath = account?.ProfileImagePath ?? string.Empty,
                LastVisitDisplay = lastVisit.HasValue ? $"Last visit: {lastVisit.Value:MMM dd, yyyy}" : string.Empty
            });
        }

        HasPatients = Patients.Count > 0;
    }

    [RelayCommand]
    private async Task OpenPatient(MyPatientItem patient)
        => await Shell.Current.Navigation.PushAsync(new Views.PatientDetailPage(patient.Email, patient.FullName, patient.Role, patient.IdNumber));

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}