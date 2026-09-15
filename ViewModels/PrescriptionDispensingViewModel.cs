using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DispenseListItem : ObservableObject
{
    public int PrescriptionId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
}

public partial class PrescriptionDispensingViewModel : ObservableObject
{
    public ObservableCollection<DispenseListItem> PendingDispense { get; } = new();

    [ObservableProperty]
    private bool hasPending;

    public async Task RefreshAsync()
    {
        var prescriptions = await Services.PrescriptionService.Instance.GetClinicGivenUndispensedAsync();

        PendingDispense.Clear();
        foreach (var rx in prescriptions)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(rx.PatientEmail);

            PendingDispense.Add(new DispenseListItem
            {
                PrescriptionId = rx.Id,
                PatientName = patient?.FullName ?? rx.PatientEmail,
                MedicineName = rx.MedicineName,
                Dosage = rx.Dosage,
                DateDisplay = rx.DatePrescribed.ToString("MMM dd, yyyy")
            });
        }

        HasPending = PendingDispense.Count > 0;
    }

    [RelayCommand]
    private async Task MarkDispensed(DispenseListItem item)
    {
        bool confirm = await Shell.Current.DisplayAlert("Mark as Dispensed?", $"Confirm {item.MedicineName} was given to {item.PatientName}?", "Confirm", "Cancel");
        if (!confirm) return;

        await Services.PrescriptionService.Instance.MarkDispensedAsync(item.PrescriptionId);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
