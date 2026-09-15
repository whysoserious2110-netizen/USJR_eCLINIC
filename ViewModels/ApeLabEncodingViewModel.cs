using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;


namespace USJR_eCLINIC.ViewModels;

public partial class ApeLabEncodingViewModel : ObservableObject
{
    public ObservableCollection<PatientOption> Patients { get; } = new();

    [ObservableProperty] private PatientOption? selectedPatient;
    [ObservableProperty] private string cbc = string.Empty;
    [ObservableProperty] private string urinalysis = string.Empty;
    [ObservableProperty] private string chestXray = string.Empty;
    [ObservableProperty] private string ecg = string.Empty;
    [ObservableProperty] private string drugTest = string.Empty;
    [ObservableProperty] private string overallFindings = string.Empty;

    public ApeLabEncodingViewModel()
    {
        _ = LoadPatientsAsync();
    }

    private async Task LoadPatientsAsync()
    {
        var patients = await Services.AuthService.Instance.GetAllPatientsAsync();

        // APE is for Faculty/Admin/Non-Teaching only, per SRS
        var eligible = patients.Where(p => p.Role is "Faculty" or "Admin Personnel" or "Non-Teaching");

        Patients.Clear();
        foreach (var p in eligible)
        {
            Patients.Add(new PatientOption
            {
                Email = p.Email,
                DisplayName = $"{p.FullName} — {p.Role}"
            });
        }
    }

    [RelayCommand]
    private async Task SaveResult()
    {
        if (SelectedPatient == null)
        {
            await Shell.Current.DisplayAlert("Missing info", "Please select a patient.", "OK");
            return;
        }

        var nurse = Services.AuthService.Instance.CurrentUser;

        var result = new Models.ApeLabResult
        {
            PatientEmail = SelectedPatient.Email,
            Cbc = Cbc,
            Urinalysis = Urinalysis,
            ChestXray = ChestXray,
            Ecg = Ecg,
            DrugTest = DrugTest,
            OverallFindings = OverallFindings,
            EncodedBy = nurse?.FullName ?? "Nurse",
            DateEncoded = DateTime.Now
        };

        await Services.ApeLabResultService.Instance.AddAsync(result);

        await Services.NotificationService.Instance.AddAsync(
            SelectedPatient.Email,
            "APE Lab Results Available",
            "Your Annual Physical Examination lab results have been encoded and are ready for review.");

        await Shell.Current.DisplayAlert("Saved", "APE lab results have been recorded.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}