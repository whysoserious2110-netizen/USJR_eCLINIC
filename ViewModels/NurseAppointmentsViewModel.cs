using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace USJR_eCLINIC.ViewModels;

public partial class NurseAppointmentItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientIdNumber { get; set; } = string.Empty;
    public string PatientIdDisplay => string.IsNullOrWhiteSpace(PatientIdNumber) ? "ID unavailable" : $"ID: {PatientIdNumber}";
    public string TimeDisplay { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string SubService { get; set; } = string.Empty;
    public string RawStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;

    public bool CanRecordVitals => RawStatus is "CheckedIn" or "VitalsRecorded";

    public string ActionText => RawStatus switch
    {
        "CheckedIn" => "Record vitals",
        "VitalsRecorded" => "Review / update vitals",
        _ => "Waiting for check-in"
    };
}

public partial class NurseAppointmentsViewModel : ObservableObject
{
    private readonly string? _statusFilter;

    public ObservableCollection<NurseAppointmentItem> TodaysAppointments { get; } = new();

    [ObservableProperty] private bool hasAppointments;
    [ObservableProperty] private string pageTitle = "Today's Triage Queue";
    [ObservableProperty] private string queueDescription = "Today's appointments across medical and dental services.";
    [ObservableProperty] private string emptyMessage = "No patients are in this queue today.";

    public NurseAppointmentsViewModel(string? statusFilter = null)
    {
        _statusFilter = statusFilter;
        if (statusFilter == "CheckedIn")
        {
            PageTitle = "Awaiting Vitals";
            QueueDescription = "Checked-in patients ready for nurse triage.";
            EmptyMessage = "No checked-in patients are waiting for vitals.";
        }
        else if (statusFilter == "VitalsRecorded")
        {
            PageTitle = "Vitals Recorded";
            QueueDescription = "Patients whose vitals have been entered.";
            EmptyMessage = "No patients have recorded vitals today.";
        }
        else if (statusFilter == "Completed")
        {
            PageTitle = "Completed Visits";
            QueueDescription = "Appointments completed today.";
            EmptyMessage = "No visits have been completed today.";
        }
    }

    public async Task RefreshAsync()
    {
        if (Services.AuthService.Instance.CurrentUser?.Role != "Nurse")
        {
            TodaysAppointments.Clear();
            HasAppointments = false;
            EmptyMessage = "The triage queue is available to Nurses only.";
            return;
        }

        var allAppointments = await Services.AppointmentService.Instance.GetAllAppointmentsAsync();
        var appointments = allAppointments
            .Where(appointment =>
                appointment.VisitDate.Date == DateTime.Today &&
                (appointment.Status is "Confirmed" or "CheckedIn" or "VitalsRecorded" or "Completed") &&
                (appointment.ServiceType == "Medical" || appointment.ServiceType == "Dental") &&
                (_statusFilter == null || appointment.Status == _statusFilter))
            .OrderBy(appointment => GetVisitTimeOfDay(appointment.VisitTime))
            .ToList();

        TodaysAppointments.Clear();
        foreach (var appointment in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appointment.PatientEmail);
            TodaysAppointments.Add(new NurseAppointmentItem
            {
                AppointmentId = appointment.Id,
                PatientEmail = appointment.PatientEmail,
                PatientName = patient?.FullName ?? appointment.PatientEmail,
                PatientIdNumber = patient?.IdNumber ?? string.Empty,
                TimeDisplay = appointment.VisitTime,
                ServiceType = appointment.ServiceType,
                SubService = string.IsNullOrWhiteSpace(appointment.ReasonOrPurpose)
                    ? appointment.SubService
                    : appointment.ReasonOrPurpose,
                RawStatus = appointment.Status,
                Status = appointment.Status switch
                {
                    "Confirmed" => "Awaiting check-in",
                    "CheckedIn" => "Awaiting nurse vitals",
                    "VitalsRecorded" => "Ready for Doctor",
                    "Completed" => "Visit completed",
                    _ => appointment.Status
                },
                StatusColor = appointment.Status switch
                {
                    "Confirmed" => Color.FromArgb("#D9A441"),
                    "CheckedIn" => Color.FromArgb("#2E6FDB"),
                    "VitalsRecorded" => Color.FromArgb("#087A3D"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    _ => Color.FromArgb("#D9A441")
                }
            });
        }

        HasAppointments = TodaysAppointments.Count > 0;
    }

    private static TimeSpan GetVisitTimeOfDay(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.TimeOfDay
            : TimeSpan.MaxValue;

    [RelayCommand]
    private async Task OpenAppointment(NurseAppointmentItem? item)
    {
        if (item == null) return;

        if (Services.AuthService.Instance.CurrentUser?.Role != "Nurse")
        {
            await Shell.Current.DisplayAlert("Access denied", "Only Nurses can access the triage workflow.", "OK");
            return;
        }

        if (!item.CanRecordVitals)
        {
            await Shell.Current.DisplayAlert(
                "Patient not checked in",
                "Vital signs can only be recorded after check-in.",
                "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(
            new Views.RecordVitalsPage(item.AppointmentId, item.PatientEmail, item.PatientName, item.PatientIdNumber));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
