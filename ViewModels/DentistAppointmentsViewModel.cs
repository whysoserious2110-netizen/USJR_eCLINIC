using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentistAppointmentItem : ObservableObject
{
    public string PatientName { get; set; } = string.Empty;
    public string DentalService { get; set; } = string.Empty;
    public DateTime VisitDate { get; set; }
    public string DateDisplay { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
}

public partial class DentistAppointmentsViewModel : ObservableObject
{
    private List<DentistAppointmentItem> _all = new();

    public ObservableCollection<DentistAppointmentItem> Appointments { get; } = new();

    [ObservableProperty] private bool hasAppointments;
    [ObservableProperty] private string selectedTab = "Upcoming";

    public bool IsAllTab => SelectedTab == "All";
    public bool IsUpcomingTab => SelectedTab == "Upcoming";
    public bool IsPastTab => SelectedTab == "Past";

    public async Task RefreshAsync()
    {
        var appointments = await Services.AppointmentService.Instance.GetAllForServiceAsync("Dental");

        _all.Clear();
        foreach (var appt in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            _all.Add(new DentistAppointmentItem
            {
                PatientName = patient?.FullName ?? appt.PatientEmail,
                DentalService = appt.SubService,
                VisitDate = appt.VisitDate,
                DateDisplay = appt.VisitDate.ToString("MMM dd, yyyy"),
                TimeDisplay = appt.VisitTime,
                Status = appt.Status,
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

        IEnumerable<DentistAppointmentItem> filtered = SelectedTab switch
        {
            "Upcoming" => _all.Where(a => a.VisitDate.Date >= today && a.Status == "Confirmed")
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
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}