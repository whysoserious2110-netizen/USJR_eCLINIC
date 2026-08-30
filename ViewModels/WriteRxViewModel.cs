using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PatientOption : ObservableObject
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty; // "Juan Dela Cruz — Student"
}

public partial class WriteRxViewModel : ObservableObject
{
    public ObservableCollection<PatientOption> Patients { get; } = new();

    [ObservableProperty]
    private PatientOption? selectedPatient;

    [ObservableProperty]
    private string medicineName = string.Empty;

    [ObservableProperty]
    private string dosage = string.Empty;

    [ObservableProperty]
    private string instructions = string.Empty;

    [ObservableProperty]
    private bool isClinicGiven = true;

    public WriteRxViewModel()
    {
        _ = LoadPatientsAsync();
    }

    private async Task LoadPatientsAsync()
    {
        var patients = await Services.AuthService.Instance.GetAllPatientsAsync();

        Patients.Clear();
        foreach (var p in patients)
        {
            Patients.Add(new PatientOption
            {
                Email = p.Email,
                DisplayName = $"{p.FullName} — {p.Role}"
            });
        }
    }

    [RelayCommand]
    private void ToggleGivenType() => IsClinicGiven = !IsClinicGiven;

    [RelayCommand]
    private async Task SavePrescription()
    {
        if (SelectedPatient == null)
        {
            await Shell.Current.DisplayAlert("Missing info", "Please select a patient.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(MedicineName) || string.IsNullOrWhiteSpace(Dosage))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter medicine name and dosage.", "OK");
            return;
        }

        var doctor = Services.AuthService.Instance.CurrentUser;

        var prescription = new Models.Prescription
        {
            PatientEmail = SelectedPatient.Email,
            DatePrescribed = DateTime.Now,
            MedicineName = MedicineName,
            Dosage = Dosage,
            Instructions = Instructions,
            IsClinicGiven = IsClinicGiven,
            PrescribedBy = doctor?.FullName ?? "Doctor"
        };

        await Services.PrescriptionService.Instance.AddAsync(prescription);

        await Services.NotificationService.Instance.AddAsync(
            SelectedPatient.Email,
            "New Prescription",
            $"Dr. {doctor?.FullName} has prescribed {MedicineName} ({Dosage}). Check your Prescriptions for details.");

        await Shell.Current.DisplayAlert("Saved", "Prescription has been recorded.", "OK");

        MedicineName = string.Empty;
        Dosage = string.Empty;
        Instructions = string.Empty;
        IsClinicGiven = true;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}