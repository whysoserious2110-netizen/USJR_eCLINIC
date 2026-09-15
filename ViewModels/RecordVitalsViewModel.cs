
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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

    public string PatientName { get; }

    [ObservableProperty] private string bloodPressure = string.Empty;
    [ObservableProperty] private string heartRate = string.Empty;
    [ObservableProperty] private string temperature = string.Empty;
    [ObservableProperty] private string respiratoryRate = string.Empty;
    [ObservableProperty] private string oxygenSaturation = string.Empty;
    [ObservableProperty] private string height = string.Empty;
    [ObservableProperty] private string weight = string.Empty;
    [ObservableProperty] private string initialAssessment = string.Empty;
    [ObservableProperty] private string nursingNotes = string.Empty;

    public ObservableCollection<VitalsHistoryItem> History { get; } = new();

    [ObservableProperty] private bool hasHistory;

    public RecordVitalsViewModel(string patientEmail, string patientName)
    {
        _patientEmail = patientEmail;
        PatientName = patientName;
        _ = LoadHistoryAsync();
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
    }

    [RelayCommand]
    private async Task SaveVitals()
    {
        if (string.IsNullOrWhiteSpace(BloodPressure) && string.IsNullOrWhiteSpace(HeartRate) && string.IsNullOrWhiteSpace(Temperature))
        {
            await Shell.Current.DisplayAlert("Missing info", "Please record at least one vital sign.", "OK");
            return;
        }

        var nurse = Services.AuthService.Instance.CurrentUser;

        var vitals = new Models.VitalSigns
        {
            PatientEmail = _patientEmail,
            BloodPressure = BloodPressure,
            HeartRate = HeartRate,
            Temperature = Temperature,
            RespiratoryRate = RespiratoryRate,
            OxygenSaturation = OxygenSaturation,
            Height = Height,
            Weight = Weight,
            InitialAssessment = InitialAssessment,
            NursingNotes = NursingNotes,
            RecordedBy = nurse?.FullName ?? "Nurse",
            DateRecorded = DateTime.Now
        };

        await Services.VitalSignsService.Instance.AddAsync(vitals);

        BloodPressure = HeartRate = Temperature = RespiratoryRate = OxygenSaturation = Height = Weight = InitialAssessment = NursingNotes = string.Empty;

        await LoadHistoryAsync();
        await Shell.Current.DisplayAlert("Saved", "Vital signs and notes have been recorded.", "OK");
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}