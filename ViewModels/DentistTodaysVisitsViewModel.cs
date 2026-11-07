using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class DentistVisitItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientRole { get; set; } = string.Empty;
    public string PatientIdNumber { get; set; } = string.Empty;
    public string PatientIdDisplay => string.IsNullOrWhiteSpace(PatientIdNumber) ? "ID unavailable" : $"ID: {PatientIdNumber}";
    public string TimeDisplay { get; set; } = string.Empty;
    public string DentalService { get; set; } = string.Empty;
    public string AppointmentContext { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string ActionMessage { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanExamine { get; set; }
    public bool IsCompleted { get; set; }
}

public partial class DentistTodaysVisitsViewModel : ObservableObject
{
    private readonly string? _statusFilter;

    [ObservableProperty] private string pageTitle = "Today's Dental Visits";
    [ObservableProperty] private string pageSubtitle = "Start an exam only after Nurse triage is complete";
    [ObservableProperty] private string emptyMessage = "No dental visits today";

    public ObservableCollection<DentistVisitItem> Visits { get; } = new();

    [ObservableProperty] private bool hasVisits;

    public DentistTodaysVisitsViewModel(string? statusFilter = null)
    {
        _statusFilter = statusFilter;
        (PageTitle, PageSubtitle, EmptyMessage) = statusFilter switch
        {
            "Confirmed" => ("Awaiting Check-in", "Confirmed dental visits scheduled for today", "No visits are awaiting check-in."),
            "CheckedIn" => ("Awaiting Nurse Triage", "Checked-in visits waiting for Nurse vitals", "No visits are waiting for Nurse triage."),
            "VitalsRecorded" => ("Ready for Dentist", "Patients are ready for dental examination", "No visits are ready for examination."),
            "Completed" => ("Completed", "Dental visits completed today", "No dental visits have been completed today."),
            _ => ("Today's Dental Visits", "Start an exam only after Nurse triage is complete", "No dental visits today")
        };
    }

    public async Task RefreshAsync()
    {
        var dentalAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Dental");
        var certificateAppointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Cert Request");
        var appointments = dentalAppointments
            .Concat(certificateAppointments.Where(appointment => appointment.SubService == "Dental Certificate"))
            .Where(appointment => string.IsNullOrWhiteSpace(_statusFilter) || appointment.Status == _statusFilter)
            .OrderBy(Services.AppointmentService.GetVisitDateTime)
            .ToList();

        Visits.Clear();
        foreach (var appointment in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appointment.PatientEmail);
            var canExamine = appointment.Status == "VitalsRecorded";
            var isCompleted = appointment.Status == "Completed";

            Visits.Add(new DentistVisitItem
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
                AppointmentContext = string.IsNullOrWhiteSpace(appointment.ReasonOrPurpose)
                    ? appointment.SubService
                    : $"{appointment.SubService} · {appointment.ReasonOrPurpose}",
                StatusDisplay = appointment.Status switch
                {
                    "Pending" => "Pending request",
                    "Confirmed" => "Awaiting check-in",
                    "CheckedIn" => "Waiting for Nurse triage",
                    "VitalsRecorded" => "Ready for Dentist",
                    "Completed" => "Completed",
                    _ => appointment.Status
                },
                ActionMessage = appointment.Status switch
                {
                    "Pending" => "Awaiting booking confirmation",
                    "Confirmed" => "The patient must check in first",
                    "CheckedIn" => "Nurse must record vitals before the exam",
                    "Completed" => "Dental examination saved",
                    _ => string.Empty
                },
                CanExamine = canExamine,
                IsCompleted = isCompleted,
                StatusColor = appointment.Status switch
                {
                    "VitalsRecorded" => Color.FromArgb("#087A3D"),
                    "Confirmed" => Color.FromArgb("#2C9057"),
                    "CheckedIn" => Color.FromArgb("#3B78B4"),
                    "Completed" => Color.FromArgb("#67766D"),
                    _ => Color.FromArgb("#C18A20")
                }
            });
        }

        HasVisits = Visits.Count > 0;
    }

    [RelayCommand]
    private async Task OpenExamination(DentistVisitItem? item)
    {
        if (item == null)
            return;

        var appointment = await Services.AppointmentService.Instance.GetByIdAsync(item.AppointmentId);
        if (Services.AuthService.Instance.CurrentUser?.Role != "Dentist" ||
            appointment == null ||
            appointment.Status != "VitalsRecorded" ||
            appointment.VisitDate.Date != DateTime.Today)
        {
            await Shell.Current.DisplayAlert(
                "Nurse triage required",
                "The Dentist can start the examination only after the Nurse records the patient’s vitals for today’s visit.",
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
    private async Task OpenCompletedRecord(DentistVisitItem? item)
    {
        if (item == null || !item.IsCompleted)
            return;

        if (Services.AuthService.Instance.CurrentUser?.Role != "Dentist")
        {
            await Shell.Current.DisplayAlert("Access denied", "Only a Dentist can view this clinical workflow record.", "OK");
            return;
        }

        var appointment = await Services.AppointmentService.Instance.GetByIdAsync(item.AppointmentId);
        var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(item.PatientEmail);
        var records = await Services.DentalRecordService.Instance.GetForPatientAsync(item.PatientEmail);
        var record = records.FirstOrDefault(saved => saved.AppointmentId == item.AppointmentId);

        if (appointment?.Status != "Completed" || patient == null || record == null)
        {
            await Shell.Current.DisplayAlert(
                "Dental record unavailable",
                "A saved dental record linked to this completed appointment could not be found.",
                "OK");
            await RefreshAsync();
            return;
        }

        var followUps = await Services.AppointmentService.Instance.GetAllForServiceAsync("Dental");
        var followUp = followUps
            .Where(candidate =>
                candidate.PatientEmail.Equals(item.PatientEmail, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.SubService, "Dental Follow-up", StringComparison.OrdinalIgnoreCase) &&
                candidate.VisitDate.Date >= DateTime.Today &&
                (candidate.Status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded"))
            .OrderBy(candidate => candidate.VisitDate)
            .FirstOrDefault();

        var recordItem = DentalRecordsViewModel.CreateListItem(record, patient, appointment, followUp, true);
        await Shell.Current.Navigation.PushAsync(new Views.DentalRecordDetailPage(recordItem));
    }

    [RelayCommand]
    private async Task GoToHome() => await Shell.Current.Navigation.PushAsync(new Views.DentistDashboardPage());

    [RelayCommand]
    private async Task GoToPatients() => await Shell.Current.Navigation.PushAsync(new Views.PatientsListPage("Dental"));

    [RelayCommand]
    private async Task GoToAppointments() => await Shell.Current.Navigation.PushAsync(new Views.DentistAppointmentsPage());

    [RelayCommand]
    private async Task GoToProfile() => await Shell.Current.Navigation.PushAsync(new Views.DentistProfilePage());

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
