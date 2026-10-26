using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class ApeLabResultItem : ObservableObject
{
    public string Category { get; set; } = string.Empty;
    public string ResultTitle { get; set; } = string.Empty;
    public string ResultText { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    public string EncodedBy { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientRole { get; set; } = string.Empty;
    public string OverallFindings { get; set; } = string.Empty;
    public bool IsOverallCard { get; set; }
    public bool HasOverallFindings => !string.IsNullOrWhiteSpace(OverallFindings);
    public bool ShowOverallFindings => !IsOverallCard && HasOverallFindings;
    public bool HasEncodedBy => !string.IsNullOrWhiteSpace(EncodedBy);

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task GoToHome() => await Shell.Current.GoToAsync("//HomePage");

    [RelayCommand]
    private async Task GoToAppointments() => await Shell.Current.GoToAsync("//AppointmentsPage");

    [RelayCommand]
    private async Task GoToNotifications() => await Shell.Current.GoToAsync("//NotificationsPage");

    [RelayCommand]
    private async Task GoToProfile() => await Shell.Current.GoToAsync("//ProfilePage");
}

public partial class ApeLabResultsViewModel : ObservableObject
{
    public ObservableCollection<ApeLabResultItem> Results { get; } = new();

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

            var user = Services.AuthService.Instance.CurrentUser;
            if (user == null)
            {
                await Shell.Current.DisplayAlert("Session expired", "Please sign in again.", "OK");
                return;
            }

            var isEligible = user.Role is "Faculty" or "Admin Personnel" or "Non-Teaching";
            if (!isEligible)
            {
                await Shell.Current.DisplayAlert(
                    "Not available",
                    "APE lab results are available only to Faculty, Admin Personnel, and Non-Teaching Personnel.",
                    "OK");
                await Shell.Current.Navigation.PopAsync();
                return;
            }

            PatientName = user.FullName;
            PatientRole = user.Role;

            var savedResults = await Services.ApeLabResultService.Instance.GetForPatientAsync(user.Email);
            Results.Clear();

            foreach (var result in savedResults)
            {
                var dateDisplay = result.DateEncoded.ToString("MMM dd, yyyy");
                var encodedBy = string.IsNullOrWhiteSpace(result.EncodedBy) ? "Clinic Nurse" : result.EncodedBy;

                AddResultIfPresent("BLOOD TEST", "Complete Blood Count", result.Cbc,
                    dateDisplay, encodedBy, user.FullName, user.Role, result.OverallFindings);
                AddResultIfPresent("URINALYSIS", "Urinalysis", result.Urinalysis,
                    dateDisplay, encodedBy, user.FullName, user.Role, result.OverallFindings);
                AddResultIfPresent("IMAGING", "Chest X-Ray", result.ChestXray,
                    dateDisplay, encodedBy, user.FullName, user.Role, result.OverallFindings);
                AddResultIfPresent("CARDIAC TEST", "Electrocardiogram (ECG)", result.Ecg,
                    dateDisplay, encodedBy, user.FullName, user.Role, result.OverallFindings);
                AddResultIfPresent("DRUG TEST", "Drug Test", result.DrugTest,
                    dateDisplay, encodedBy, user.FullName, user.Role, result.OverallFindings);

                if (!string.IsNullOrWhiteSpace(result.OverallFindings))
                {
                    Results.Add(new ApeLabResultItem
                    {
                        Category = "APE SUMMARY",
                        ResultTitle = "Overall Findings",
                        ResultText = result.OverallFindings.Trim(),
                        DateDisplay = dateDisplay,
                        EncodedBy = encodedBy,
                        PatientName = user.FullName,
                        PatientRole = user.Role,
                        OverallFindings = result.OverallFindings.Trim(),
                        IsOverallCard = true
                    });
                }
            }

            HasResults = Results.Count > 0;
            HasNoResults = !HasResults;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void AddResultIfPresent(
        string category,
        string title,
        string? resultValue,
        string dateDisplay,
        string encodedBy,
        string patientName,
        string patientRole,
        string? overallFindings)
    {
        if (string.IsNullOrWhiteSpace(resultValue))
            return;

        Results.Add(new ApeLabResultItem
        {
            Category = category,
            ResultTitle = title,
            ResultText = resultValue.Trim(),
            DateDisplay = dateDisplay,
            EncodedBy = encodedBy,
            PatientName = patientName,
            PatientRole = patientRole,
            OverallFindings = overallFindings?.Trim() ?? string.Empty
        });
    }

    [RelayCommand]
    private async Task OpenResult(ApeLabResultItem item)
    {
        await Shell.Current.Navigation.PushAsync(new Views.ApeLabResultDetailPage(item));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task GoToHome() => await Shell.Current.GoToAsync("//HomePage");

    [RelayCommand]
    private async Task GoToAppointments() => await Shell.Current.GoToAsync("//AppointmentsPage");

    [RelayCommand]
    private async Task GoToNotifications() => await Shell.Current.GoToAsync("//NotificationsPage");

    [RelayCommand]
    private async Task GoToProfile() => await Shell.Current.GoToAsync("//ProfilePage");
}
