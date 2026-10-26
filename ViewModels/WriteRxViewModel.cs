using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class MedicineEntry : ObservableObject
{
    [ObservableProperty] private string medicineName = string.Empty;
    [ObservableProperty] private string dosage = string.Empty;
    [ObservableProperty] private string quantity = "1";
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
    private readonly string? _presetPatientEmail;

    public ObservableCollection<PatientOption> Patients { get; } = new();

    [ObservableProperty] private PatientOption? selectedPatient;
    [ObservableProperty] private bool hasPresetPatient;
    [ObservableProperty] private bool hasEligiblePatients;
    [ObservableProperty] private string presetPatientName = string.Empty;

    public ObservableCollection<MedicineEntry> Medicines { get; } = new();

    public WriteRxViewModel(string? patientEmail = null)
    {
        _presetPatientEmail = patientEmail;
        Medicines.Add(new MedicineEntry());
        _ = LoadPatientsAsync();
    }

    private async Task LoadPatientsAsync()
    {
        var allPatients = await Services.AuthService.Instance.GetAllPatientsAsync();
        var eligibleEmails = new HashSet<string>(
            await Services.MedicalRecordService.Instance.GetPatientEmailsWithConsultationsAsync(),
            StringComparer.OrdinalIgnoreCase);

        var todaysMedicalVisits = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Medical");
        var todaysCertificateVisits = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Cert Request");

        foreach (var appointment in todaysMedicalVisits.Concat(todaysCertificateVisits))
        {
            var isMedicalCertificateVisit =
                appointment.ServiceType == "Medical" ||
                (appointment.ServiceType == "Cert Request" &&
                 appointment.SubService == "Medical Certificate");

            if (isMedicalCertificateVisit &&
                appointment.Status is "CheckedIn" or "VitalsRecorded")
            {
                eligibleEmails.Add(appointment.PatientEmail);
            }
        }

        // A prescription opened from a consultation must keep that consultation's patient available,
        // even when this is the patient's first visit and no consultation record exists yet.
        if (!string.IsNullOrWhiteSpace(_presetPatientEmail))
            eligibleEmails.Add(_presetPatientEmail);

        var eligiblePatients = allPatients
            .Where(patient => eligibleEmails.Contains(patient.Email))
            .OrderBy(patient => patient.FullName)
            .ToList();

        Patients.Clear();
        foreach (var patient in eligiblePatients)
        {
            Patients.Add(new PatientOption
            {
                Email = patient.Email,
                DisplayName = $"{patient.FullName} — {patient.Role}"
            });
        }

        HasEligiblePatients = Patients.Count > 0;

        if (!string.IsNullOrWhiteSpace(_presetPatientEmail))
        {
            SelectedPatient = Patients.FirstOrDefault(patient =>
                patient.Email.Equals(_presetPatientEmail, StringComparison.OrdinalIgnoreCase));

            if (SelectedPatient != null)
            {
                HasPresetPatient = true;
                PresetPatientName = SelectedPatient.DisplayName;
            }
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

        var validEntries = Medicines
            .Where(medicine =>
                !string.IsNullOrWhiteSpace(medicine.MedicineName) &&
                !string.IsNullOrWhiteSpace(medicine.Dosage))
            .ToList();

        if (validEntries.Count == 0)
        {
            await Shell.Current.DisplayAlert(
                "Missing info",
                "Please enter at least one medicine with a dosage.",
                "OK");
            return;
        }

        foreach (var medicine in validEntries)
        {
            if (!int.TryParse(medicine.Quantity, out var quantity) || quantity <= 0)
            {
                await Shell.Current.DisplayAlert(
                    "Invalid quantity",
                    $"Enter a positive whole-number quantity for {medicine.MedicineName}.",
                    "OK");
                return;
            }
        }

        var doctor = Services.AuthService.Instance.CurrentUser;
        var now = DateTime.Now;

        foreach (var medicine in validEntries)
        {
            int.TryParse(medicine.Quantity, out var quantity);

            var prescription = new Models.Prescription
            {
                PatientEmail = SelectedPatient.Email,
                DatePrescribed = now,
                MedicineName = medicine.MedicineName.Trim(),
                Dosage = medicine.Dosage.Trim(),
                Quantity = quantity,
                Instructions = medicine.Instructions.Trim(),
                IsClinicGiven = medicine.IsClinicGiven,
                PrescribedBy = doctor?.FullName ?? "Doctor"
            };

            await Services.PrescriptionService.Instance.AddAsync(prescription);
        }

        var medicineNames = string.Join(", ", validEntries.Select(medicine => medicine.MedicineName));

        await Services.NotificationService.Instance.AddAsync(
            SelectedPatient.Email,
            "New Prescription",
            $"Dr. {doctor?.FullName} has prescribed: {medicineNames}. Check your Prescriptions for details.");

        await Shell.Current.DisplayAlert("Saved", "Prescription has been recorded.", "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}