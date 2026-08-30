using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PrescriptionListItem : ObservableObject
{
    public string DateDisplay { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string LabelText { get; set; } = string.Empty;   // "Clinic-Given" or "Take-Home"
    public Color LabelColor { get; set; } = Colors.Gray;
}

public partial class PrescriptionsViewModel : ObservableObject
{
    public ObservableCollection<PrescriptionListItem> Prescriptions { get; } = new();

    [ObservableProperty]
    private bool hasPrescriptions;

    public PrescriptionsViewModel()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var records = await Services.PrescriptionService.Instance.GetForPatientAsync(user.Email);

        Prescriptions.Clear();
        foreach (var p in records)
        {
            Prescriptions.Add(new PrescriptionListItem
            {
                DateDisplay = p.DatePrescribed.ToString("MMM dd, yyyy"),
                MedicineName = p.MedicineName,
                Dosage = p.Dosage,
                Instructions = p.Instructions,
                LabelText = p.IsClinicGiven ? "Clinic-Given" : "Take-Home",
                LabelColor = p.IsClinicGiven ? Color.FromArgb("#0F9B8E") : Color.FromArgb("#D9A441")
            });
        }

        HasPrescriptions = Prescriptions.Count > 0;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}