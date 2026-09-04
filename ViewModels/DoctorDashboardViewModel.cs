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


public partial class CertRequestItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
}



public partial class DoctorDashboardViewModel : ObservableObject
{


    public ObservableCollection<CertRequestItem> PendingCertRequests { get; } = new();

    [ObservableProperty]
    private bool hasCertRequests;



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

    [ObservableProperty]
    private int unreadNotificationCount;

    [ObservableProperty]
    private bool hasUnreadNotifications;

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null) return;

        DoctorName = user.FullName;



        UnreadNotificationCount = await Services.NotificationService.Instance.GetUnreadCountAsync(user.Email);
        HasUnreadNotifications = UnreadNotificationCount > 0;

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


        var certRequests = await Services.AppointmentService.Instance.GetCertRequestsAsync();

        PendingCertRequests.Clear();
        foreach (var appt in certRequests)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            PendingCertRequests.Add(new CertRequestItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                CertificateType = appt.SubService,
                Purpose = appt.ReasonOrPurpose,
                DateDisplay = appt.VisitDate.ToString("MMM dd, yyyy")
            });
        }
        HasCertRequests = PendingCertRequests.Count > 0;
    }

    [RelayCommand]
    private async Task GoToTodaysVisits()
    => await Shell.Current.Navigation.PushAsync(new Views.TodaysVisitsPage());

    [RelayCommand]
    private async Task GoToNotifications()
    => await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());


    [RelayCommand]
    private async Task GoToPatientRecords()
    => await Shell.Current.Navigation.PushAsync(new Views.MyPatientsPage());

    [RelayCommand]
    private async Task GoToWriteRx()
    => await Shell.Current.Navigation.PushAsync(new Views.WriteRxPage());

    [RelayCommand]
    private async Task OpenIssueCertificate(CertRequestItem item)
    {
        await Shell.Current.Navigation.PushAsync(new Views.IssueCertificatePage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.CertificateType, item.Purpose));
    }



    [RelayCommand]
    private async Task GoToAnnouncements()
    => await Shell.Current.Navigation.PushAsync(new Views.PostAnnouncementPage());

    [RelayCommand]
    private async Task GoToPatients()
    => await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage());

    [RelayCommand]
    private async Task GoToAppointments()
    => await Shell.Current.Navigation.PushAsync(new Views.DoctorAppointmentsPage());

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