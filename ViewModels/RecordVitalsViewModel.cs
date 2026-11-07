using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace USJR_eCLINIC.ViewModels;

public partial class VitalsHistoryItem : ObservableObject
{
    public string DateDisplay { get; set; } = string.Empty;
    public string BloodPressure { get; set; } = string.Empty;
    public string HeartRate { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = string.Empty;
    public string NursingNotes { get; set; } = string.Empty;
}

public partial class RecordVitalsViewModel : ObservableObject
{
    private readonly string _patientEmail;
    private readonly int _appointmentId;
    private bool _initialized;
    private bool _isInitializing;

    public string PatientName { get; }
    public string PatientIdNumber { get; }
    public string PatientIdentityText => string.IsNullOrWhiteSpace(PatientIdNumber)
        ? "Patient ID unavailable"
        : $"ID: {PatientIdNumber}";

    [ObservableProperty] private string bloodPressure = string.Empty;
    [ObservableProperty] private string heartRate = string.Empty;
    [ObservableProperty] private string temperature = string.Empty;
    [ObservableProperty] private string respiratoryRate = string.Empty;
    [ObservableProperty] private string oxygenSaturation = string.Empty;
    [ObservableProperty] private string height = string.Empty;
    [ObservableProperty] private string weight = string.Empty;
    [ObservableProperty] private string initialAssessment = string.Empty;
    [ObservableProperty] private string nursingNotes = string.Empty;
    [ObservableProperty] private bool isSaving;

    public ObservableCollection<VitalsHistoryItem> History { get; } = new();
    [ObservableProperty] private bool hasHistory;

    public RecordVitalsViewModel(int appointmentId, string patientEmail, string patientName, string patientIdNumber = "")
    {
        _appointmentId = appointmentId;
        _patientEmail = patientEmail;
        PatientName = patientName;
        PatientIdNumber = patientIdNumber;
    }

    public async Task InitializeAsync()
    {
        if (_initialized || _isInitializing)
            return;

        _isInitializing = true;
        try
        {
            if (Services.AuthService.Instance.CurrentUser?.Role != "Nurse")
            {
                await Shell.Current.DisplayAlert("Access denied", "Only Nurses can record triage vital signs.", "OK");
                await Shell.Current.Navigation.PopAsync();
                return;
            }

            if (!await IsEligibleAppointmentAsync())
            {
                await Shell.Current.DisplayAlert(
                    "Visit is not in triage",
                    "Vitals can only be recorded for a checked-in appointment scheduled for today. Refresh the Nurse queue and confirm the patient’s status.",
                    "OK");
                await Shell.Current.Navigation.PopAsync();
                return;
            }

            await LoadHistoryAsync();
            _initialized = true;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task<bool> IsEligibleAppointmentAsync()
    {
        var appointment = await Services.AppointmentService.Instance.GetByIdAsync(_appointmentId);
        return appointment != null &&
               appointment.VisitDate.Date == DateTime.Today &&
               (appointment.Status is "CheckedIn" or "VitalsRecorded") &&
               appointment.PatientEmail.Equals(_patientEmail, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadHistoryAsync()
    {
        var records = await Services.VitalSignsService.Instance.GetForPatientAsync(_patientEmail);
        History.Clear();
        foreach (var v in records)
        {
            History.Add(new VitalsHistoryItem
            {
                DateDisplay = v.DateRecorded.ToString("MMM dd, yyyy · h:mm tt"),
                BloodPressure = v.BloodPressure,
                HeartRate = v.HeartRate,
                Temperature = v.Temperature,
                RecordedBy = v.RecordedBy,
                NursingNotes = v.NursingNotes
            });
        }
        HasHistory = History.Count > 0;

        var latestForVisit = records
            .Where(record => record.AppointmentId == _appointmentId)
            .OrderByDescending(record => record.DateRecorded)
            .FirstOrDefault();
        if (latestForVisit != null)
        {
            BloodPressure = latestForVisit.BloodPressure;
            HeartRate = latestForVisit.HeartRate;
            Temperature = latestForVisit.Temperature;
            RespiratoryRate = latestForVisit.RespiratoryRate;
            OxygenSaturation = latestForVisit.OxygenSaturation;
            Height = latestForVisit.Height;
            Weight = latestForVisit.Weight;
            InitialAssessment = latestForVisit.InitialAssessment;
            NursingNotes = latestForVisit.NursingNotes;
        }
    }

    [RelayCommand]
    private async Task SaveVitals()
    {
        if (IsSaving)
            return;

        await InitializeAsync();
        if (!_initialized)
            return;

        if (string.IsNullOrWhiteSpace(BloodPressure) ||
            string.IsNullOrWhiteSpace(HeartRate) ||
            string.IsNullOrWhiteSpace(Temperature))
        {
            await Shell.Current.DisplayAlert(
                "Complete the core vitals",
                "Enter blood pressure, heart rate, and temperature before marking this patient ready for the Doctor. Respiratory rate, oxygen saturation, height, and weight may be entered when available.",
                "OK");
            return;
        }

        if (!IsValidBloodPressure(BloodPressure) ||
            !IsPositiveNumber(HeartRate, "bpm") ||
            !IsPositiveNumber(Temperature, "°C", "C") ||
            !IsOptionalPositiveNumber(RespiratoryRate, "breaths/min", "/min") ||
            !IsOptionalOxygenSaturation(OxygenSaturation) ||
            !IsOptionalPositiveNumber(Height, "cm") ||
            !IsOptionalPositiveNumber(Weight, "kg"))
        {
            await Shell.Current.DisplayAlert(
                "Check the vital sign values",
                "Use positive numeric values. Enter blood pressure as systolic/diastolic (for example, 120/80); oxygen saturation must be 100% or lower.",
                "OK");
            return;
        }

        if (!await IsEligibleAppointmentAsync())
        {
            await Shell.Current.DisplayAlert(
                "Visit status changed",
                "This appointment is no longer eligible for triage. The record was not saved. Refresh the Nurse queue and confirm the patient’s current status.",
                "OK");
            return;
        }

        IsSaving = true;
        try
        {
            var nurse = Services.AuthService.Instance.CurrentUser;
            var vitals = new Models.VitalSigns
            {
                AppointmentId = _appointmentId,
                PatientEmail = _patientEmail,
                BloodPressure = BloodPressure.Trim(),
                HeartRate = HeartRate.Trim(),
                Temperature = Temperature.Trim(),
                RespiratoryRate = RespiratoryRate.Trim(),
                OxygenSaturation = OxygenSaturation.Trim(),
                Height = Height.Trim(),
                Weight = Weight.Trim(),
                InitialAssessment = InitialAssessment.Trim(),
                NursingNotes = NursingNotes.Trim(),
                RecordedBy = nurse?.FullName ?? string.Empty,
                DateRecorded = DateTime.Now
            };

            var saved = await Services.VitalSignsService.Instance.SaveForCheckedInAppointmentAsync(vitals);
            if (!saved)
            {
                await Shell.Current.DisplayAlert(
                    "Vitals were not saved",
                    "The appointment changed before the save completed. Refresh the Nurse queue and try again only if the visit is still checked in.",
                    "OK");
                return;
            }

            await LoadHistoryAsync();
            await Shell.Current.DisplayAlert(
                "Triage saved",
                "Vitals and nursing notes were recorded. This patient is now marked ready for the Doctor.",
                "OK");
        }
        catch
        {
            await Shell.Current.DisplayAlert("Save failed", "The triage record could not be saved. Please try again.", "OK");
        }
        finally
        {
            IsSaving = false;
        }
    }

    private static bool IsValidBloodPressure(string value)
    {
        var normalized = RemoveUnits(value, "mmHg");
        var parts = normalized.Split('/', StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
               TryPositive(parts[0], out _) &&
               TryPositive(parts[1], out _);
    }

    private static bool IsPositiveNumber(string value, params string[] units)
        => TryPositive(RemoveUnits(value, units), out _);

    private static bool IsOptionalPositiveNumber(string value, params string[] units)
        => string.IsNullOrWhiteSpace(value) || IsPositiveNumber(value, units);

    private static bool IsOptionalOxygenSaturation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return TryPositive(RemoveUnits(value, "%", "percent"), out var result) && result <= 100;
    }

    private static string RemoveUnits(string value, params string[] units)
    {
        var normalized = value.Trim();
        foreach (var unit in units.OrderByDescending(unit => unit.Length))
            normalized = normalized.Replace(unit, string.Empty, StringComparison.OrdinalIgnoreCase);
        return string.Concat(normalized.Where(character => !char.IsWhiteSpace(character)));
    }

    private static bool TryPositive(string value, out decimal result)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result) && result > 0;

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
