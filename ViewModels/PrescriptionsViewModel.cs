using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PrescriptionListItem : ObservableObject
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public string Instructions { get; set; } = string.Empty;
    public string PrescribedBy { get; set; } = string.Empty;
    public string LabelText { get; set; } = string.Empty;
    public Color LabelColor { get; set; } = Colors.Gray;
    public Color LabelBackgroundColor { get; set; } = Colors.White;
    public Color LabelForegroundColor { get; set; } = Colors.Gray;
    public bool IsClinicGiven { get; set; }
    public bool IsDispensed { get; set; }
    public DateTime? DispensedDate { get; set; }
    public string DispensedDateDisplay => DispensedDate?.ToString("MMM dd, yyyy · h:mm tt") ?? string.Empty;
    public bool HasInstructions => !string.IsNullOrWhiteSpace(Instructions);
    public bool HasPrescriber => !string.IsNullOrWhiteSpace(PrescribedBy);
    public bool HasDispensedDate => DispensedDate.HasValue;
    public string DispensingStatus => IsDispensed ? "Dispensed by the clinic" : "Awaiting clinic dispensing";

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
        foreach (var prescription in records)
        {
            var isClinicGiven = prescription.IsClinicGiven;
            Prescriptions.Add(new PrescriptionListItem
            {
                Id = prescription.Id,
                DateDisplay = prescription.DatePrescribed.ToString("MMM dd, yyyy"),
                MedicineName = prescription.MedicineName,
                Dosage = prescription.Dosage,
                Quantity = prescription.Quantity,
                Instructions = prescription.Instructions,
                PrescribedBy = prescription.PrescribedBy,
                LabelText = isClinicGiven ? "Clinic-Given" : "Take-Home",
                LabelColor = isClinicGiven ? Color.FromArgb("#0F9B8E") : Color.FromArgb("#D9A441"),
                LabelBackgroundColor = isClinicGiven ? Color.FromArgb("#D5F5EF") : Color.FromArgb("#FFF1C2"),
                LabelForegroundColor = isClinicGiven ? Color.FromArgb("#087A3D") : Color.FromArgb("#946200"),
                IsClinicGiven = isClinicGiven,
                IsDispensed = prescription.IsDispensed,
                DispensedDate = prescription.DispensedDate
            });
        }

        HasPrescriptions = Prescriptions.Count > 0;
    }

    [RelayCommand]
    private async Task OpenPrescription(PrescriptionListItem item)
    {
        await Shell.Current.Navigation.PushAsync(new Views.PrescriptionDetailPage(item));
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
