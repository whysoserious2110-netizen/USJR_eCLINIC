using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PatientListItem : ObservableObject
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string ProfileImagePath { get; set; } = string.Empty;
}

public partial class PatientsListViewModel : ObservableObject
{
    private readonly string? _serviceTypeFilter;
    private readonly bool _consultedOnly;
    private List<PatientListItem> _allPatients = new();

    public ObservableCollection<PatientListItem> Patients { get; } = new();

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private bool hasPatients;
    [ObservableProperty] private string listTitle = "Patients";
    [ObservableProperty] private string listDescription = "Registered clinic patients";
    [ObservableProperty] private string emptyMessage = "No patients found. Try another name or ID.";
    [ObservableProperty] private string resultCountText = "0 patients";

    public PatientsListViewModel(string? serviceTypeFilter = null, bool consultedOnly = false)
    {
        _serviceTypeFilter = serviceTypeFilter;
        _consultedOnly = consultedOnly;

        if (_consultedOnly)
        {
            ListTitle = "Patient Records";
            ListDescription = "Patients with a saved medical or dental consultation";
            EmptyMessage = "No consultation records yet. Patients appear here after a medical or dental record is saved.";
        }
        else if (!string.IsNullOrWhiteSpace(_serviceTypeFilter))
        {
            ListDescription = $"Patients with {serviceTypeFilter.ToLowerInvariant()} appointments";
        }
    }

    public async Task RefreshAsync()
    {
        var patients = await Services.AuthService.Instance.GetAllPatientsAsync();

        if (!string.IsNullOrEmpty(_serviceTypeFilter))
        {
            var appointments = await Services.AppointmentService.Instance
                .GetAllForServiceAsync(_serviceTypeFilter);
            var eligibleEmails = appointments
                .Where(appointment => appointment.Status != "Cancelled")
                .Select(appointment => appointment.PatientEmail)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            patients = patients.Where(patient => eligibleEmails.Contains(patient.Email)).ToList();
        }

        if (_consultedOnly)
        {
            var medicalEmails = await Services.MedicalRecordService.Instance
                .GetPatientEmailsWithConsultationsAsync();
            var dentalEmails = await Services.DentalRecordService.Instance
                .GetPatientEmailsWithRecordsAsync();
            var consultedEmails = medicalEmails
                .Concat(dentalEmails)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            patients = patients.Where(patient => consultedEmails.Contains(patient.Email)).ToList();
        }

        _allPatients = patients.Select(patient => new PatientListItem
        {
            Email = patient.Email,
            FullName = patient.FullName,
            Role = patient.Role,
            IdNumber = patient.IdNumber,
            ProfileImagePath = patient.ProfileImagePath
        }).ToList();

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allPatients
            : _allPatients.Where(patient =>
                patient.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                patient.IdNumber.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        Patients.Clear();
        foreach (var patient in filtered)
            Patients.Add(patient);

        HasPatients = Patients.Count > 0;
        ResultCountText = Patients.Count == 1 ? "1 patient" : $"{Patients.Count} patients";
        if (Patients.Count == 0)
        {
            EmptyMessage = _allPatients.Count == 0 && _consultedOnly
                ? "No consultation records yet. Patients appear here after a medical or dental record is saved."
                : _allPatients.Count == 0
                    ? "No patients found."
                    : "No patient records match your search.";
        }
    }

    [RelayCommand]
    private async Task OpenPatient(PatientListItem patient)
        => await Shell.Current.Navigation.PushAsync(
            new Views.PatientDetailPage(patient.Email, patient.FullName, patient.Role, patient.IdNumber));

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
