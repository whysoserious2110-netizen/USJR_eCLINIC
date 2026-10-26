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
    public string VisitLabel { get; set; } = "Medical Consultation";
    public string VisitStatus { get; set; } = string.Empty;
}

public partial class CertRequestItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientRole { get; set; } = string.Empty;
    public string CertificateType { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    public string AppointmentStatus { get; set; } = string.Empty;
}

public partial class DoctorDashboardViewModel : ObservableObject
{
    public ObservableCollection<QueueItem> ImmediateQueue { get; } = new();
    public ObservableCollection<CertRequestItem> PendingCertRequests { get; } = new();

    [ObservableProperty] private string doctorName = string.Empty;
    [ObservableProperty] private string announcementText = string.Empty;
    [ObservableProperty] private bool hasAnnouncement;
    [ObservableProperty] private int todayVisitsCount;
    [ObservableProperty] private int readyForConsultationCount;
    [ObservableProperty] private int certificateReviewCount;
    [ObservableProperty] private bool hasQueueItems;
    [ObservableProperty] private bool hasCertRequests;
    [ObservableProperty] private int unreadNotificationCount;
    [ObservableProperty] private bool hasUnreadNotifications;

    public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null)
            return;

        DoctorName = user.FullName;
        UnreadNotificationCount = await Services.NotificationService.Instance.GetUnreadCountAsync(user.Email);
        HasUnreadNotifications = UnreadNotificationCount > 0;

        var announcement = await Services.AnnouncementService.Instance.GetActiveForRoleAsync("Doctor");
        HasAnnouncement = announcement != null;
        AnnouncementText = announcement?.Message ?? string.Empty;

        var medicalAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Medical");
        TodayVisitsCount = medicalAppointments.Count;

        // R.E.A.D.S. approves and checks in patients. Only checked-in visits enter the consultation queue.
        var certificateAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Cert Request");
        var readyAppointments = medicalAppointments
            .Where(IsReadyForConsultation)
            .Concat(certificateAppointments.Where(appointment =>
                appointment.SubService == "Medical Certificate" && IsReadyForConsultation(appointment)))
            .OrderBy(appointment => appointment.QueueNumber ?? int.MaxValue)
            .ThenBy(Services.AppointmentService.GetVisitDateTime)
            .ToList();

        ImmediateQueue.Clear();
        var fallbackQueueNumber = 1;
        foreach (var appointment in readyAppointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appointment.PatientEmail);
            var isCertificateVisit = appointment.ServiceType == "Cert Request";

            ImmediateQueue.Add(new QueueItem
            {
                AppointmentId = appointment.Id,
                PatientEmail = appointment.PatientEmail,
                TimeDisplay = appointment.VisitTime,
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                PatientRole = patient?.Role ?? string.Empty,
                ChiefComplaint = string.IsNullOrWhiteSpace(appointment.ReasonOrPurpose)
                    ? appointment.SubService
                    : appointment.ReasonOrPurpose,
                QueueNumber = appointment.QueueNumber ?? fallbackQueueNumber,
                Location = appointment.Location,
                VisitLabel = isCertificateVisit ? "Medical Certificate Visit" : "Medical Consultation",
                VisitStatus = appointment.Status == "VitalsRecorded" ? "Vitals recorded" : "Checked in"
            });

            fallbackQueueNumber++;
        }

        ReadyForConsultationCount = ImmediateQueue.Count;
        HasQueueItems = ImmediateQueue.Count > 0;

        var certificateRequests = await Services.AppointmentService.Instance
            .GetCertificateRequestsForClinicalReviewAsync(user.Role);

        PendingCertRequests.Clear();
        foreach (var appointment in certificateRequests)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appointment.PatientEmail);
            PendingCertRequests.Add(new CertRequestItem
            {
                AppointmentId = appointment.Id,
                PatientEmail = appointment.PatientEmail,
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                CertificateType = appointment.SubService,
                Purpose = appointment.ReasonOrPurpose,
                DateDisplay = appointment.VisitDate.ToString("MMM dd, yyyy")
            });
        }

        CertificateReviewCount = PendingCertRequests.Count;
        HasCertRequests = PendingCertRequests.Count > 0;
    }

    private static bool IsReadyForConsultation(Models.Appointment appointment) =>
        appointment.Status is "CheckedIn" or "VitalsRecorded";

    [RelayCommand]
    private async Task GoToTodaysVisits()
        => await Shell.Current.Navigation.PushAsync(new Views.TodaysVisitsPage());

    [RelayCommand]
    private async Task GoToCertificates()
        => await Shell.Current.Navigation.PushAsync(new Views.CertificatesPage());

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
        if (item == null)
            return;

        await Shell.Current.Navigation.PushAsync(new Views.IssueCertificatePage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.CertificateType, item.Purpose));
    }

    [RelayCommand]
    private async Task GoToAppointments()
        => await Shell.Current.Navigation.PushAsync(new Views.DoctorAppointmentsPage());

    [RelayCommand]
    private async Task GoToProfile()
        => await Shell.Current.Navigation.PushAsync(new Views.DoctorProfilePage());

    [RelayCommand]
    private async Task OpenConsultation(QueueItem item)
    {
        if (item == null)
            return;

        var appointment = await Services.AppointmentService.Instance.GetByIdAsync(item.AppointmentId);
        if (appointment == null || !IsReadyForConsultation(appointment))
        {
            await Shell.Current.DisplayAlert(
                "Not ready for consultation",
                "The patient must be checked in by R.E.A.D.S. before the doctor can start the visit.",
                "OK");
            await RefreshAsync();
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.ConsultationPage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.PatientRole, item.ChiefComplaint));
    }
}