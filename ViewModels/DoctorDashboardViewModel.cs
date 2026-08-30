using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class QueueItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientRole { get; set; } = string.Empty;
    public string ChiefComplaint { get; set; } = string.Empty;
    public int QueueNumber { get; set; }
    public string Location { get; set; } = "Main Campus Clinic";
}

public partial class DoctorDashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private string doctorName = string.Empty;

    [ObservableProperty]
    private string announcementText = string.Empty;

    [ObservableProperty]
    private bool hasAnnouncement;

    [ObservableProperty]
    private int todayVisitsCount;

    public ObservableCollection<QueueItem> ImmediateQueue { get; } = new();

    [ObservableProperty]
    private bool hasQueueItems;

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        DoctorName = user.FullName;

        var announcement = await Services.AnnouncementService.Instance.GetActiveForRoleAsync("Doctor");
        if (announcement != null)
        {
            HasAnnouncement = true;
            AnnouncementText = announcement.Message;
        }
        else
        {
            HasAnnouncement = false;
        }

        var todaysAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Medical");
        TodayVisitsCount = todaysAppointments.Count;

        ImmediateQueue.Clear();
        int queueNum = 1;

        foreach (var appt in todaysAppointments.Where(a => a.Status == "Pending" || a.Status == "Confirmed"))
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            ImmediateQueue.Add(new QueueItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                TimeDisplay = appt.VisitTime,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                PatientRole = patient?.Role ?? string.Empty,
                ChiefComplaint = string.IsNullOrWhiteSpace(appt.ReasonOrPurpose) ? appt.SubService : appt.ReasonOrPurpose,
                QueueNumber = queueNum++,
                Location = appt.Location
            }); ;
        }

        HasQueueItems = ImmediateQueue.Count > 0;
    }

    [RelayCommand]
    private async Task GoToTodaysVisits()
    => await Shell.Current.Navigation.PushAsync(new Views.TodaysVisitsPage());

    [RelayCommand]
    private async Task GoToPatientRecords()
    => await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage());

    [RelayCommand]
    private async Task GoToWriteRx()
    => await Shell.Current.Navigation.PushAsync(new Views.WriteRxPage());


    [RelayCommand]
    private async Task GoToConsultation()
    {
        if (ImmediateQueue.Count == 0)
        {
            await Shell.Current.DisplayAlert("No patients in queue", "There are no patients waiting for consultation today.", "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.ConsultationPage(
            ImmediateQueue[0].AppointmentId, ImmediateQueue[0].PatientEmail,
            ImmediateQueue[0].PatientName, ImmediateQueue[0].PatientRole, ImmediateQueue[0].ChiefComplaint));
    }

    [RelayCommand]
    private async Task GoToPatients()
    => await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage());

    [RelayCommand]
    private async Task GoToAppointments()
        => await Shell.Current.DisplayAlert("Appointments", "Coming soon.", "OK");

    [RelayCommand]
    
    private async Task GoToProfile()
    => await Shell.Current.Navigation.PushAsync(new Views.DoctorProfilePage());


    [RelayCommand]
    private async Task OpenConsultation(QueueItem item)
    {
        await Shell.Current.Navigation.PushAsync(new Views.ConsultationPage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.PatientRole, item.ChiefComplaint));
    }
}