using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DispenseListItem : ObservableObject
{
    public int PrescriptionId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientEmail { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
}

public partial class PrescriptionDispensingViewModel : ObservableObject
{
    private List<DispenseListItem> _allPending = new();

    public ObservableCollection<DispenseListItem> PendingDispense { get; } = new();

    [ObservableProperty] private bool hasPending;
    [ObservableProperty] private string pendingCountText = "No prescriptions awaiting dispensing";
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string emptyMessage = "There are no clinic-given prescriptions waiting to be dispensed.";

    public async Task RefreshAsync()
    {
        var prescriptions = await Services.PrescriptionService.Instance.GetClinicGivenUndispensedAsync();
        _allPending = new List<DispenseListItem>();

        foreach (var prescription in prescriptions)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(prescription.PatientEmail);
            _allPending.Add(new DispenseListItem
            {
                PrescriptionId = prescription.Id,
                PatientName = patient?.FullName ?? prescription.PatientEmail,
                PatientEmail = prescription.PatientEmail,
                MedicineName = prescription.MedicineName,
                Dosage = prescription.Dosage,
                Quantity = prescription.Quantity > 0 ? prescription.Quantity : 1,
                DateDisplay = prescription.DatePrescribed.ToString("MMM d, yyyy · h:mm tt")
            });
        }

        HasPending = _allPending.Count > 0;
        PendingCountText = _allPending.Count == 1
            ? "1 prescription waiting"
            : $"{_allPending.Count} prescriptions waiting";
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var matches = string.IsNullOrWhiteSpace(query)
            ? _allPending
            : _allPending.Where(item =>
                item.PatientName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.PatientEmail.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.MedicineName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        PendingDispense.Clear();
        foreach (var item in matches)
            PendingDispense.Add(item);

        EmptyMessage = _allPending.Count == 0
            ? "There are no clinic-given prescriptions waiting to be dispensed."
            : "No pending prescriptions match your search.";
    }

    [RelayCommand]
    private async Task MarkDispensed(DispenseListItem item)
    {
        var confirmed = await Shell.Current.DisplayAlert(
            "Confirm dispensing",
            $"Confirm that {item.Quantity} unit(s) of {item.MedicineName} were given to {item.PatientName}? Inventory will be updated.",
            "Confirm",
            "Cancel");

        if (!confirmed) return;

        var result = await Services.PrescriptionService.Instance.MarkDispensedAsync(item.PrescriptionId);
        await Shell.Current.DisplayAlert(
            result.Success ? "Dispensing complete" : "Cannot dispense",
            result.Message,
            "OK");

        if (result.Success)
            await RefreshAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
