using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class NurseAppointmentItem : ObservableObject
{
    public int AppointmentId { get; set; }

    public string PatientEmail { get; set; } = string.Empty;

    public string PatientName { get; set; } = string.Empty;

    public string TimeDisplay { get; set; } = string.Empty;

    public string ServiceType { get; set; } = string.Empty;

    public string SubService { get; set; } = string.Empty;

    public string RawStatus { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Color StatusColor { get; set; } = Colors.Gray;

    public bool CanRecordVitals =>
        RawStatus == "CheckedIn" ||
        RawStatus == "VitalsRecorded";

    public string ActionText => RawStatus switch
    {
        "CheckedIn" => "Record Vitals",
        "VitalsRecorded" => "View / Update Vitals",
        _ => "Waiting for Check-In"
    };
}

public partial class NurseAppointmentsViewModel : ObservableObject
{
    public ObservableCollection<NurseAppointmentItem>
        TodaysAppointments
    { get; } = new();

    [ObservableProperty]
    private bool hasAppointments;

    public async Task RefreshAsync()
    {
        var allAppointments = await Services.AppointmentService.Instance
            .GetAllAppointmentsAsync();

        var appointments = allAppointments
            .Where(a =>
                a.VisitDate.Date == DateTime.Today &&
                a.Status != "Cancelled" &&
                (a.ServiceType == "Medical" ||
                 a.ServiceType == "Dental"))
            .OrderBy(a => a.VisitTime)
            .ToList();

        TodaysAppointments.Clear();

        foreach (var appt in appointments)
        {
            var patient = await Services.AuthService.Instance
                .GetAccountByEmailAsync(appt.PatientEmail);

            TodaysAppointments.Add(new NurseAppointmentItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                PatientName =
                    patient?.FullName ?? appt.PatientEmail,
                TimeDisplay = appt.VisitTime,
                ServiceType = appt.ServiceType,
                SubService =
                    string.IsNullOrWhiteSpace(appt.ReasonOrPurpose)
                        ? appt.SubService
                        : appt.ReasonOrPurpose,
                RawStatus = appt.Status,

                Status = appt.Status switch
                {
                    "CheckedIn" => "Checked In",
                    "VitalsRecorded" => "Vitals Recorded",
                    _ => appt.Status
                },

                StatusColor = appt.Status switch
                {
                    "Confirmed" =>
                        Color.FromArgb("#0F9B8E"),

                    "CheckedIn" =>
                        Color.FromArgb("#2E6FDB"),

                    "VitalsRecorded" =>
                        Color.FromArgb("#7B61C8"),

                    "Completed" =>
                        Color.FromArgb("#4A90D9"),

                    _ =>
                        Color.FromArgb("#D9A441")
                }
            });
        }

        HasAppointments = TodaysAppointments.Count > 0;
    }

    [RelayCommand]
    private async Task OpenAppointment(
        NurseAppointmentItem? item)
    {
        if (item == null)
            return;

        var currentUser =
            Services.AuthService.Instance.CurrentUser;

        if (currentUser?.Role != "Nurse")
        {
            await Shell.Current.DisplayAlert(
                "Access denied",
                "Only Nurses can access the triage workflow.",
                "OK");

            return;
        }

        if (item.RawStatus != "CheckedIn" &&
            item.RawStatus != "VitalsRecorded")
        {
            await Shell.Current.DisplayAlert(
                "Patient not checked in",
                "Vital signs can only be recorded after check-in.",
                "OK");

            return;
        }

        await Shell.Current.Navigation.PushAsync(
            new Views.RecordVitalsPage(
                item.AppointmentId,
                item.PatientEmail,
                item.PatientName));
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}