using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DoctorAppointmentItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientRole { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string ChiefComplaint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanConsult { get; set; }
}

public partial class DoctorAppointmentsViewModel : ObservableObject
{
    private List<DoctorAppointmentItem> _all = new();

    public ObservableCollection<DoctorAppointmentItem> Appointments { get; } = new();

    [ObservableProperty]
    private bool hasAppointments;

    [ObservableProperty]
    private string selectedTab = "Upcoming";

    public bool IsAllTab => SelectedTab == "All";
    public bool IsUpcomingTab => SelectedTab == "Upcoming";
    public bool IsPastTab => SelectedTab == "Past";

    public async Task RefreshAsync()
    {
        var appointments = await Services.AppointmentService.Instance.GetAllForServiceAsync("Medical");

        _all.Clear();

        foreach (var appt in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            _all.Add(new DoctorAppointmentItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                PatientRole = patient?.Role ?? string.Empty,
                VisitDate = appt.VisitDate,
                DateDisplay = appt.VisitDate.ToString("MMM dd, yyyy"),
                TimeDisplay = appt.VisitTime,
                ChiefComplaint = string.IsNullOrWhiteSpace(appt.ReasonOrPurpose) ? appt.SubService : appt.ReasonOrPurpose,
                Status = appt.Status,
                CanConsult = appt.Status == "Pending" || appt.Status == "Confirmed",
                StatusColor = appt.Status switch
                {
                    "Confirmed" => Color.FromArgb("#0F9B8E"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    "Cancelled" => Color.FromArgb("#E05B5B"),
                    _ => Color.FromArgb("#D9A441")
                }
            });
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var today = DateTime.Today;

        IEnumerable<DoctorAppointmentItem> filtered = SelectedTab switch
        {
            "Upcoming" => _all.Where(a => a.VisitDate.Date >= today && a.Status != "Cancelled" && a.Status != "Completed")
                               .OrderBy(a => a.VisitDate),
            "Past" => _all.Where(a => a.VisitDate.Date < today || a.Status == "Completed" || a.Status == "Cancelled")
                           .OrderByDescending(a => a.VisitDate),
            _ => _all.OrderByDescending(a => a.VisitDate)
        };

        Appointments.Clear();
        foreach (var item in filtered)
            Appointments.Add(item);

        HasAppointments = Appointments.Count > 0;
    }

    [RelayCommand]
    private void SelectTab(string tab)
    {
        SelectedTab = tab;
        OnPropertyChanged(nameof(IsAllTab));
        OnPropertyChanged(nameof(IsUpcomingTab));
        OnPropertyChanged(nameof(IsPastTab));
        ApplyFilter();
    }

    [RelayCommand]
    private async Task OpenConsultation(DoctorAppointmentItem item)
    {
        if (!item.CanConsult)
        {
            await Shell.Current.DisplayAlert("Not available", $"This appointment is already {item.Status}.", "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.ConsultationPage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.PatientRole, item.ChiefComplaint));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}