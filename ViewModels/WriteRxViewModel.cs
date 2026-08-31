using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class MedicineEntry : ObservableObject
{
    [ObservableProperty] private string medicineName = string.Empty;
    [ObservableProperty] private string dosage = string.Empty;
    [ObservableProperty] private string instructions = string.Empty;
    [ObservableProperty] private bool isClinicGiven = true;

    [RelayCommand]
    private void ToggleGivenType() => IsClinicGiven = !IsClinicGiven;
}

public partial class PatientOption : ObservableObject
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public partial class WriteRxViewModel : ObservableObject
{
    public ObservableCollection<PatientOption> Patients { get; } = new();

    [ObservableProperty]
    private PatientOption? selectedPatient;

    public ObservableCollection<MedicineEntry> Medicines { get; } = new();

    public WriteRxViewModel()
    {
        Medicines.Add(new MedicineEntry());
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
    private void AddAnotherMedicine() => Medicines.Add(new MedicineEntry());

    [RelayCommand]
    private void RemoveMedicine(MedicineEntry entry)
    {
        if (Medicines.Count > 1)
            Medicines.Remove(entry);
    }

    [RelayCommand]
    private async Task SavePrescription()
    {
        if (SelectedPatient == null)
        {
            await Shell.Current.DisplayAlert("Missing info", "Please select a patient.", "OK");
            return;
        }

        var validEntries = Medicines.Where(m => !string.IsNullOrWhiteSpace(m.MedicineName) && !string.IsNullOrWhiteSpace(m.Dosage)).ToList();

        if (validEntries.Count == 0)
        {
            await Shell.Current.DisplayAlert("Missing info", "Please enter at least one medicine with a dosage.", "OK");
            return;
        }

        var doctor = Services.AuthService.Instance.CurrentUser;
        var now = DateTime.Now;

        foreach (var med in validEntries)
        {
            var prescription = new Models.Prescription
            {
                PatientEmail = SelectedPatient.Email,
                DatePrescribed = now,
                MedicineName = med.MedicineName,
                Dosage = med.Dosage,
                Instructions = med.Instructions,
                IsClinicGiven = med.IsClinicGiven,
                PrescribedBy = doctor?.FullName ?? "Doctor"
            };

            await Services.PrescriptionService.Instance.AddAsync(prescription);
        }

        var medicineNames = string.Join(", ", validEntries.Select(m => m.MedicineName));

        await Services.NotificationService.Instance.AddAsync(
            SelectedPatient.Email,
            "New Prescription",
            $"Dr. {doctor?.FullName} has prescribed: {medicineNames}. Check your Prescriptions for details.");

        await Shell.Current.DisplayAlert("Saved", "Prescription has been recorded.", "OK");

        // Pop back to Doctor Dashboard specifically (1 level back from Write Rx)
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}