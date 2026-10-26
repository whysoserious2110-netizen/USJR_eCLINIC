using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalRecordListItem : ObservableObject
{
    public int Id { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string ExamFindings { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string TreatmentRendered { get; set; } = string.Empty;
    public string CareInstructions { get; set; } = string.Empty;
    public string AttendingDentist { get; set; } = string.Empty;
    public string DentalNotes { get; set; } = string.Empty;
    public bool HasCareInstructions => !string.IsNullOrWhiteSpace(CareInstructions);
    public bool HasAttendingDentist => !string.IsNullOrWhiteSpace(AttendingDentist);
    public bool HasDentalNotes => !string.IsNullOrWhiteSpace(DentalNotes);

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

public partial class DentalRecordsViewModel : ObservableObject
{
    public ObservableCollection<DentalRecordListItem> Records { get; } = new();

    [ObservableProperty]
    private bool hasRecords;

    public DentalRecordsViewModel()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        var records = await Services.DentalRecordService.Instance.GetForPatientAsync(user.Email);

        Records.Clear();
        foreach (var record in records)
        {
            Records.Add(new DentalRecordListItem
            {
                Id = record.Id,
                DateDisplay = record.VisitDate.ToString("MMM dd, yyyy"),
                ExamFindings = record.ExamFindings,
                Diagnosis = record.Diagnosis,
                TreatmentRendered = record.TreatmentRendered,
                CareInstructions = record.CareInstructions,
                AttendingDentist = record.AttendingDentist,
                DentalNotes = record.DentalNotes
            });
        }

        HasRecords = Records.Count > 0;
    }

    [RelayCommand]
    private async Task ViewDetails(DentalRecordListItem item)
    {
        await Shell.Current.Navigation.PushAsync(new Views.DentalRecordDetailPage(item));
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
