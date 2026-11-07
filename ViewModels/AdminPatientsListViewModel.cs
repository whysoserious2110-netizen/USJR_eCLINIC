using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class AdminPatientsListViewModel : ObservableObject
{
    private List<PatientListItem> _allPatients = new();

    public ObservableCollection<PatientListItem> Patients { get; } = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool hasPatients;

    [ObservableProperty]
    private bool canRegisterPatients;

    public bool HasSearched => !string.IsNullOrWhiteSpace(SearchText);

    public AdminPatientsListViewModel()
    {
        CanRegisterPatients =
            Services.AuthService.Instance.CurrentUser?.Role == "R.E.A.D.S. Scholar";
    }

    public async Task RefreshAsync()
    {
        var patients = await Services.AuthService.Instance.GetAllPatientsAsync();

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

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearched));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Patients.Clear();

        if (!HasSearched)
        {
            HasPatients = false;
            return;
        }

        var filtered = _allPatients.Where(p =>
                p.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                p.IdNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
              .ToList();

        foreach (var p in filtered)
            Patients.Add(p);

        HasPatients = Patients.Count > 0;
    }

    [RelayCommand]
    private async Task RegisterPatient()
    {
        if (Services.AuthService.Instance.CurrentUser?.Role != "R.E.A.D.S. Scholar")
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only R.E.A.D.S. Scholars can register clinic patients.",
                "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.RegisterPatientPage());
    }
    [RelayCommand]
    private async Task OpenPatient(PatientListItem patient)
        => await Shell.Current.Navigation.PushAsync(new Views.AdminPatientPage(patient.Email));

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}