using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentalQueueItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientDisplayName => string.IsNullOrWhiteSpace(PatientRole) ? PatientName : $"{PatientName} ({PatientRole})";
    public string PatientRole { get; set; } = string.Empty;
    public string PatientIdNumber { get; set; } = string.Empty;
    public string PatientIdDisplay => string.IsNullOrWhiteSpace(PatientIdNumber) ? "ID unavailable" : $"ID: {PatientIdNumber}";
    public string TimeDisplay { get; set; } = string.Empty;
    public string DentalService { get; set; } = string.Empty;
    public string AppointmentContext { get; set; } = string.Empty;
    public string ReasonOrPurpose { get; set; } = string.Empty;
    public string Location { get; set; } = "Main Campus Clinic";
    public string StatusDisplay { get; set; } = "Vitals recorded";
    public string ReasonDisplay => string.IsNullOrWhiteSpace(ReasonOrPurpose) ? $"Reason: {DentalService}" : $"Reason: {ReasonOrPurpose}";
    public string LocationDisplay => string.IsNullOrWhiteSpace(Location) ? DentalService : $"{DentalService} · {Location}";
}

public partial class DentistDashboardViewModel : ObservableObject
{
    [ObservableProperty] private string dentistName = string.Empty;
    [ObservableProperty] private string clinicLocation = "Main Campus Clinic";
    [ObservableProperty] private string todayDisplay = DateTime.Today.ToString("dddd, MMM d");
    [ObservableProperty] private string announcementText = "No active clinic announcements right now.";
    [ObservableProperty] private int todayVisitsCount;
    [ObservableProperty] private int activeVisitCount;
    [ObservableProperty] private int confirmedAwaitingCheckInCount;
    [ObservableProperty] private int awaitingNurseTriageCount;
    [ObservableProperty] private int readyForDentistCount;
    [ObservableProperty] private int completedVisitCount;
    [ObservableProperty] private int followUpVisitCount;
    [ObservableProperty] private string readyQueueEmptyMessage = "No dental visits are ready for examination.";
    [ObservableProperty] private int unreadNotificationCount;
    [ObservableProperty] private bool hasUnreadNotifications;
    [ObservableProperty] private string profileImagePath = string.Empty;
    [ObservableProperty] private string selectedQuickAction = string.Empty;

    public ObservableCollection<DentalQueueItem> DentalQueue { get; } = new();
[ObservableProperty] private bool hasQueueItems;

    [ObservableProperty] private int dentalCertificateRequestCount;
public async Task RefreshAsync()
    {
        var user = Services.AuthService.Instance.CurrentUser;
        if (user == null)
            return;

        DentistName = user.FullName;
        TodayDisplay = DateTime.Today.ToString("dddd, MMM d");
        ProfileImagePath = user.ProfileImagePath;

        var announcement = await Services.AnnouncementService.Instance.GetActiveForRoleAsync("Dentist");
        AnnouncementText = announcement?.Message ?? "No active clinic announcements right now.";

        var allDentalAppointments = await Services.AppointmentService.Instance.GetAllForServiceAsync("Dental");
        FollowUpVisitCount = allDentalAppointments.Count(IsUpcomingDentalFollowUp);

        var dentalAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Dental");
        var certificateAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Cert Request");
        var todayVisits = dentalAppointments
            .Concat(certificateAppointments.Where(appointment => appointment.SubService == "Dental Certificate"))
            .OrderBy(Services.AppointmentService.GetVisitDateTime)
            .ToList();

        TodayVisitsCount = todayVisits.Count;
        ActiveVisitCount = todayVisits.Count(appointment => appointment.Status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded");
        ClinicLocation = todayVisits
            .Select(appointment => appointment.Location)
            .FirstOrDefault(location => !string.IsNullOrWhiteSpace(location))
            ?? "Main Campus Clinic";

        ConfirmedAwaitingCheckInCount = todayVisits.Count(appointment => appointment.Status == "Confirmed");
        AwaitingNurseTriageCount = todayVisits.Count(appointment => appointment.Status == "CheckedIn");
        ReadyForDentistCount = todayVisits.Count(appointment => appointment.Status == "VitalsRecorded");
        CompletedVisitCount = todayVisits.Count(appointment => appointment.Status == "Completed");

        DentalQueue.Clear();
        foreach (var appointment in todayVisits.Where(IsReadyForDentalExamination))
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appointment.PatientEmail);
            DentalQueue.Add(new DentalQueueItem
            {
                AppointmentId = appointment.Id,
                PatientEmail = appointment.PatientEmail,
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                PatientRole = patient?.Role ?? string.Empty,
                PatientIdNumber = patient?.IdNumber ?? string.Empty,
                TimeDisplay = appointment.VisitTime,
                DentalService = appointment.ServiceType == "Cert Request"
                    ? "Dental Certificate Visit"
                    : appointment.SubService,
                AppointmentContext = BuildAppointmentContext(appointment),
                ReasonOrPurpose = appointment.ReasonOrPurpose,
                Location = appointment.Location,
                StatusDisplay = "Vitals recorded · Ready"
            });
        }

        HasQueueItems = DentalQueue.Count > 0;
        ReadyQueueEmptyMessage = AwaitingNurseTriageCount > 0
            ? $"{AwaitingNurseTriageCount} checked-in visit(s) are waiting for Nurse triage."
            : "Ready visits appear here after the Nurse records the patient’s vitals.";

        var certificateRequests = await Services.AppointmentService.Instance
            .GetCertificateRequestsForClinicalReviewAsync(user.Role);
        DentalCertificateRequestCount = certificateRequests.Count;


        var notifications = await Services.NotificationService.Instance.GetForUserAsync(user.Email);
        UnreadNotificationCount = notifications.Count(notification => !notification.IsRead);
        HasUnreadNotifications = UnreadNotificationCount > 0;


    }

    private static bool IsUpcomingDentalFollowUp(Models.Appointment appointment) =>
        appointment.ServiceType == "Dental" &&
        string.Equals(appointment.SubService, "Dental Follow-up", StringComparison.OrdinalIgnoreCase) &&
        appointment.VisitDate.Date >= DateTime.Today &&
        (appointment.Status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded");
    private static string BuildAppointmentContext(Models.Appointment appointment)
    {
        var service = appointment.ServiceType == "Cert Request"
            ? "Dental Certificate Visit"
            : appointment.SubService;
        return string.IsNullOrWhiteSpace(appointment.ReasonOrPurpose)
            ? service
            : $"{service} · {appointment.ReasonOrPurpose}";
    }

    private static bool IsReadyForDentalExamination(Models.Appointment appointment) =>
        appointment.Status == "VitalsRecorded" &&
        appointment.VisitDate.Date == DateTime.Today &&
        (appointment.ServiceType == "Dental" ||
         (appointment.ServiceType == "Cert Request" && appointment.SubService == "Dental Certificate"));

    [RelayCommand]
    private async Task OpenExamination(DentalQueueItem? item)
    {
        if (item == null)
            return;

        if (Services.AuthService.Instance.CurrentUser?.Role != "Dentist")
        {
            await Shell.Current.DisplayAlert("Access denied", "Only a Dentist can start a dental examination.", "OK");
            return;
        }

        var appointment = await Services.AppointmentService.Instance.GetByIdAsync(item.AppointmentId);
        if (appointment == null || !IsReadyForDentalExamination(appointment))
        {
            await Shell.Current.DisplayAlert(
                "Nurse triage required",
                "Start the dental examination after the Nurse records the patient’s vitals.",
                "OK");
            await RefreshAsync();
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.DentalExaminationPage(
            item.AppointmentId,
            item.PatientEmail,
            item.PatientName,
            item.PatientRole,
            item.AppointmentContext));
    }

    [RelayCommand]
    private async Task GoToClinicSummary()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistClinicSummaryPage());

    [RelayCommand]
    private async Task GoToTodaysVisits()
    {
        SelectedQuickAction = "Today";
        await Shell.Current.Navigation.PushAsync(new Views.DentistTodaysVisitsPage());
    }

    [RelayCommand]
    private async Task GoToFollowUpPatients()
    {
        SelectedQuickAction = "FollowUps";
        await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage(followUpsOnly: true));
    }
    [RelayCommand]
    private async Task GoToMyPatients()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistMyPatientsPage());

    [RelayCommand]
    private async Task GoToPatients()
    {
        SelectedQuickAction = "Patients";
        await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage("Dental"));
    }

    [RelayCommand]
    private async Task GoToCertificates()
    {
        SelectedQuickAction = "Certificates";
        await Shell.Current.Navigation.PushAsync(new Views.CertificatesPage());
    }

    [RelayCommand]
    private async Task GoToAppointments()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage());

    [RelayCommand]
    private async Task GoToNotifications()
        => await Shell.Current.Navigation.PushAsync(new Views.NotificationsPage());

    [RelayCommand]
    private async Task GoToProfile()
        => await Shell.Current.Navigation.PushAsync(new Views.DentistProfilePage());
}






