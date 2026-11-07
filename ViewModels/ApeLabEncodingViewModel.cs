using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public class ApePatientOption
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string IdNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string IdNumberDisplay => string.IsNullOrWhiteSpace(IdNumber) ? "ID not recorded" : $"ID: {IdNumber}";
}

public partial class ApeLabEncodingViewModel : ObservableObject
{
    private bool _patientsLoaded;
    private bool _accessDeniedShown;

    public ObservableCollection<ApePatientOption> Patients { get; } = new();
    public ObservableCollection<ApePatientOption> FilteredPatients { get; } = new();

    [ObservableProperty] private ApePatientOption? selectedPatient;
    [ObservableProperty] private string patientSearchText = string.Empty;
    [ObservableProperty] private string cbc = string.Empty;
    [ObservableProperty] private string urinalysis = string.Empty;
    [ObservableProperty] private string chestXray = string.Empty;
    [ObservableProperty] private string ecg = string.Empty;
    [ObservableProperty] private string drugTest = string.Empty;
    [ObservableProperty] private string overallFindings = string.Empty;
    [ObservableProperty] private bool isLoadingPatients;
    [ObservableProperty] private bool hasPatients;
    [ObservableProperty] private bool hasNoPatients;
    [ObservableProperty] private bool hasSelectedPatient;
    [ObservableProperty] private bool hasSearchResults;
    [ObservableProperty] private bool hasNoSearchResults;
    [ObservableProperty] private bool isSearchPromptVisible = true;
    [ObservableProperty] private string patientLoadMessage = "Loading eligible patients...";

    partial void OnPatientSearchTextChanged(string value) => UpdatePatientSearchResults();

    partial void OnSelectedPatientChanged(ApePatientOption? value)
    {
        HasSelectedPatient = value != null;
        UpdatePatientSearchResults();
    }

    public async Task LoadPatientsAsync()
    {
        if (Services.AuthService.Instance.CurrentUser?.Role != "Nurse")
        {
            HasPatients = false;
            HasNoPatients = true;
            PatientLoadMessage = "APE result encoding is available to Nurses only.";
            if (!_accessDeniedShown)
            {
                _accessDeniedShown = true;
                await Shell.Current.DisplayAlert("Access denied", PatientLoadMessage, "OK");
            }
            return;
        }

        if (_patientsLoaded || IsLoadingPatients) return;

        try
        {
            IsLoadingPatients = true;
            HasNoPatients = false;
            PatientLoadMessage = "Loading eligible patients...";

            var patients = await Services.AuthService.Instance.GetAllPatientsAsync();
            var eligible = patients.Where(patient =>
                    patient.Role is "Faculty" or "Admin Personnel" or "Non-Teaching")
                .OrderBy(patient => patient.FullName, StringComparer.OrdinalIgnoreCase);

            Patients.Clear();
            foreach (var patient in eligible)
            {
                Patients.Add(new ApePatientOption
                {
                    Email = patient.Email,
                    FullName = patient.FullName,
                    IdNumber = patient.IdNumber,
                    Role = patient.Role
                });
            }

            HasPatients = Patients.Count > 0;
            HasNoPatients = !HasPatients;
            PatientLoadMessage = HasPatients
                ? string.Empty
                : "No eligible Faculty, Admin Personnel, or Non-Teaching patient accounts were found.";
            UpdatePatientSearchResults();
            _patientsLoaded = true;
        }
        catch
        {
            HasPatients = false;
            HasNoPatients = true;
            PatientLoadMessage = "Patient list could not load. Leave this page and try again.";
        }
        finally
        {
            IsLoadingPatients = false;
        }
    }

    private void UpdatePatientSearchResults()
    {
        FilteredPatients.Clear();
        var query = PatientSearchText.Trim();

        if (!HasPatients || HasSelectedPatient || string.IsNullOrWhiteSpace(query))
        {
            HasSearchResults = false;
            HasNoSearchResults = false;
            IsSearchPromptVisible = HasPatients && !HasSelectedPatient && string.IsNullOrWhiteSpace(query);
            return;
        }

        var matches = Patients
            .Where(patient =>
                patient.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(patient.IdNumber) &&
                 patient.IdNumber.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                patient.Role.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        foreach (var patient in matches)
            FilteredPatients.Add(patient);

        HasSearchResults = FilteredPatients.Count > 0;
        HasNoSearchResults = FilteredPatients.Count == 0;
        IsSearchPromptVisible = false;
    }

    [RelayCommand]
    private void SelectPatient(ApePatientOption patient)
    {
        if (patient == null) return;
        SelectedPatient = patient;
        PatientSearchText = string.Empty;
    }

    [RelayCommand]
    private void ChangePatient()
    {
        SelectedPatient = null;
        PatientSearchText = string.Empty;
    }

    [RelayCommand]
    private async Task SaveResult()
    {
        if (Services.AuthService.Instance.CurrentUser?.Role != "Nurse")
        {
            await Shell.Current.DisplayAlert("Access denied", "Only Nurses can encode APE laboratory results.", "OK");
            return;
        }

        if (SelectedPatient == null)
        {
            await Shell.Current.DisplayAlert("Select a patient", "Search by registered ID and select the matching eligible staff record.", "OK");
            return;
        }

        if (SelectedPatient.Role is not ("Faculty" or "Admin Personnel" or "Non-Teaching"))
        {
            await Shell.Current.DisplayAlert("Patient not eligible", "APE lab encoding is currently available for Faculty, Admin Personnel, and Non-Teaching accounts.", "OK");
            return;
        }

        if (new[] { Cbc, Urinalysis, ChestXray, Ecg, DrugTest }.All(string.IsNullOrWhiteSpace))
        {
            await Shell.Current.DisplayAlert("No test results entered", "Enter at least one result from a verified laboratory report before saving.", "OK");
            return;
        }

        var account = await Services.AuthService.Instance.GetAccountByEmailAsync(SelectedPatient.Email);
        if (account == null || account.Role != SelectedPatient.Role)
        {
            await Shell.Current.DisplayAlert("Patient record changed", "The selected account could not be verified. Search for the patient again.", "OK");
            SelectedPatient = null;
            return;
        }

        var nurse = Services.AuthService.Instance.CurrentUser;
        var result = new Models.ApeLabResult
        {
            PatientEmail = account.Email,
            Cbc = Cbc.Trim(),
            Urinalysis = Urinalysis.Trim(),
            ChestXray = ChestXray.Trim(),
            Ecg = Ecg.Trim(),
            DrugTest = DrugTest.Trim(),
            OverallFindings = OverallFindings.Trim(),
            EncodedBy = nurse?.FullName ?? "Nurse",
            DateEncoded = DateTime.Now
        };

        try
        {
            await Services.ApeLabResultService.Instance.AddAsync(result);
        }
        catch
        {
            await Shell.Current.DisplayAlert("Could not save results", "The APE lab result was not saved. Check the patient and result fields, then try again.", "OK");
            return;
        }

        var notificationSaved = true;
        try
        {
            await Services.NotificationService.Instance.AddAsync(
                account.Email,
                "APE Lab Results Available",
                "Your Annual Physical Examination lab results have been encoded and are ready for review.");
        }
        catch
        {
            notificationSaved = false;
        }

        await Shell.Current.DisplayAlert(
            "Results saved",
            notificationSaved
                ? "Verified APE laboratory results were recorded for the selected patient."
                : "Results were saved, but the patient notification could not be recorded.",
            "OK");
        await Shell.Current.Navigation.PopAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
