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
    private List<PatientListItem> _allPatients = new();

    public ObservableCollection<PatientListItem> Patients { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool hasPatients;

    public PatientsListViewModel(string? serviceTypeFilter = null)
    {
        _serviceTypeFilter = serviceTypeFilter;
    }

    public async Task RefreshAsync()
    {
        var patients = await Services.AuthService.Instance.GetAllPatientsAsync();

        if (!string.IsNullOrEmpty(_serviceTypeFilter))
        {
            var appointments = await Services.AppointmentService.Instance
                .GetAllForServiceAsync(_serviceTypeFilter);

            var eligibleEmails = appointments
                .Where(a => a.Status != "Cancelled")
                .Select(a => a.PatientEmail)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            patients = patients
                .Where(p => eligibleEmails.Contains(p.Email))
                .ToList();
        }

        _allPatients = patients.Select(p => new PatientListItem
        {
            Email = p.Email,
            FullName = p.FullName,
            Role = p.Role,
            IdNumber = p.IdNumber,
            ProfileImagePath = p.ProfileImagePath
        }).ToList();

        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allPatients
            : _allPatients.Where(p =>
                p.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.IdNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
              .ToList();

        Patients.Clear();
        foreach (var p in filtered)
            Patients.Add(p);

        HasPatients = Patients.Count > 0;
    }

    [RelayCommand]
    private async Task OpenPatient(PatientListItem patient)
        => await Shell.Current.Navigation.PushAsync(new Views.PatientDetailPage(patient.Email, patient.FullName, patient.Role, patient.IdNumber));

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}