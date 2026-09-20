using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public class ApeLabResultItem
{
    public string DateDisplay { get; set; } = string.Empty;

    public string Cbc { get; set; } = string.Empty;

    public string Urinalysis { get; set; } = string.Empty;

    public string ChestXray { get; set; } = string.Empty;

    public string Ecg { get; set; } = string.Empty;

    public string DrugTest { get; set; } = string.Empty;

    public string OverallFindings { get; set; } = string.Empty;

    public string EncodedBy { get; set; } = string.Empty;
}

public partial class ApeLabResultsViewModel : ObservableObject
{
    public ObservableCollection<ApeLabResultItem>
        Results
    { get; } = new();

    [ObservableProperty]
    private bool hasResults;

    [ObservableProperty]
    private bool hasNoResults = true;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string patientName = string.Empty;

    [ObservableProperty]
    private string patientRole = string.Empty;

    public async Task RefreshAsync()
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;

            var user =
                Services.AuthService.Instance.CurrentUser;

            if (user == null)
            {
                await Shell.Current.DisplayAlert(
                    "Session expired",
                    "Please sign in again.",
                    "OK");

                return;
            }

            var isEligible =
                user.Role == "Faculty" ||
                user.Role == "Admin Personnel" ||
                user.Role == "Non-Teaching";

            if (!isEligible)
            {
                await Shell.Current.DisplayAlert(
                    "Not available",
                    "APE lab results are available only to " +
                    "Faculty, Admin Personnel, and " +
                    "Non-Teaching Personnel.",
                    "OK");

                await Shell.Current.Navigation.PopAsync();
                return;
            }

            PatientName = user.FullName;
            PatientRole = user.Role;

            var savedResults =
                await Services.ApeLabResultService.Instance
                    .GetForPatientAsync(user.Email);

            Results.Clear();

            foreach (var result in savedResults)
            {
                Results.Add(new ApeLabResultItem
                {
                    DateDisplay = result.DateEncoded.ToString(
                        "MMMM dd, yyyy · h:mm tt"),

                    Cbc = DisplayValue(result.Cbc),

                    Urinalysis =
                        DisplayValue(result.Urinalysis),

                    ChestXray =
                        DisplayValue(result.ChestXray),

                    Ecg = DisplayValue(result.Ecg),

                    DrugTest =
                        DisplayValue(result.DrugTest),

                    OverallFindings =
                        DisplayValue(result.OverallFindings),

                    EncodedBy =
                        string.IsNullOrWhiteSpace(
                            result.EncodedBy)
                            ? "Clinic Nurse"
                            : result.EncodedBy
                });
            }

            HasResults = Results.Count > 0;
            HasNoResults = !HasResults;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string DisplayValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Not provided"
            : value.Trim();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}