using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class MedicalRecordListItem : ObservableObject
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string DateTimeDisplay { get; set; } = string.Empty;
    public string ChiefComplaint { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string Treatment { get; set; } = string.Empty;
    public string FollowUpNotes { get; set; } = string.Empty;
    public string AttendingStaff { get; set; } = string.Empty;
    public string BloodPressure { get; set; } = string.Empty;
    public string Temperature { get; set; } = string.Empty;
    public string PulseRate { get; set; } = string.Empty;
    public string RespiratoryRate { get; set; } = string.Empty;
    public bool HasFollowUpNotes => !string.IsNullOrWhiteSpace(FollowUpNotes);
    public bool HasAttendingStaff => !string.IsNullOrWhiteSpace(AttendingStaff);
    public bool HasVitals =>
        !string.IsNullOrWhiteSpace(BloodPressure) ||
        !string.IsNullOrWhiteSpace(Temperature) ||
        !string.IsNullOrWhiteSpace(PulseRate) ||
        !string.IsNullOrWhiteSpace(RespiratoryRate);

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

public partial class MedicalRecordsViewModel : ObservableObject
{
    public ObservableCollection<MedicalRecordListItem> Records { get; } = new();

    [ObservableProperty]
    private bool hasRecords;

    public MedicalRecordsViewModel()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var records = await Services.MedicalRecordService.Instance.GetForPatientAsync(user.Email);

        Records.Clear();
        foreach (var record in records)
        {
            Records.Add(new MedicalRecordListItem
            {
                Id = record.Id,
                DateDisplay = record.ConsultationDate.ToString("MMM dd, yyyy"),
                DateTimeDisplay = record.ConsultationDate.ToString("MMM dd, yyyy · h:mm tt"),
                ChiefComplaint = record.ChiefComplaint,
                Diagnosis = record.Diagnosis,
                Treatment = record.Treatment,
                FollowUpNotes = record.FollowUpNotes,
                AttendingStaff = record.AttendingStaff,
                BloodPressure = record.BloodPressure,
                Temperature = record.Temperature,
                PulseRate = record.PulseRate,
                RespiratoryRate = record.RespiratoryRate
            });
        }

        HasRecords = Records.Count > 0;
    }

    [RelayCommand]
    private async Task OpenRecord(MedicalRecordListItem record)
    {
        await Shell.Current.Navigation.PushAsync(new Views.MedicalRecordDetailPage(record));
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
