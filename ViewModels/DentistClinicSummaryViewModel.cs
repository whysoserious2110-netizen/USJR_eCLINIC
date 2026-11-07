using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalClinicStatusItem : ObservableObject
{
    public string Title { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public int Count { get; set; }
    public Color LabelColor { get; set; } = Color.FromArgb("#17221B");
}

public partial class DentistClinicSummaryViewModel : ObservableObject
{
    [ObservableProperty] private string dateDisplay = DateTime.Today.ToString("dddd, MMMM d, yyyy");

    public ObservableCollection<DentalClinicStatusItem> StatusItems { get; } = new()
    {
        new() { Title = "Awaiting to check-in", StatusCode = "Confirmed", LabelColor = Color.FromArgb("#15884E") },
        new() { Title = "Awaiting Nurse Triage", StatusCode = "CheckedIn" },
        new() { Title = "Ready for Dentist", StatusCode = "VitalsRecorded" },
        new() { Title = "Completed", StatusCode = "Completed" }
    };

    public async Task RefreshAsync()
    {
        DateDisplay = DateTime.Today.ToString("dddd, MMMM d, yyyy");

        var dentalAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Dental");
        var certificateAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Cert Request");
        var todayDentalVisits = dentalAppointments
            .Concat(certificateAppointments.Where(appointment => appointment.SubService == "Dental Certificate"))
            .ToList();

        foreach (var item in StatusItems)
            item.Count = todayDentalVisits.Count(appointment => appointment.Status == item.StatusCode);
    }

    [RelayCommand]
    private async Task OpenStatus(DentalClinicStatusItem? item)
    {
        if (item == null)
            return;

        await Shell.Current.Navigation.PushAsync(new Views.DentistTodaysVisitsPage(item.StatusCode));
    }

    [RelayCommand]
    private async Task GoToHome()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistDashboardPage());

    [RelayCommand]
    private async Task GoToPatients()
        => await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage("Dental"));

    [RelayCommand]
    private async Task GoToAppointments()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage());

    [RelayCommand]
    private async Task GoToProfile()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistProfilePage());

    [RelayCommand]
    private async Task GoBack()
        => await Shell.Current.Navigation.PopAsync();
}
