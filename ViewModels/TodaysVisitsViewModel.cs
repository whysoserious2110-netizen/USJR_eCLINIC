using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class TodaysVisitItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientEmail { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string PatientRole { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string ChiefComplaint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ActionMessage { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanConsult { get; set; }
}

public partial class TodaysVisitsViewModel : ObservableObject
{
    public ObservableCollection<TodaysVisitItem> Visits { get; } = new();

    [ObservableProperty] private bool hasVisits;

    public async Task RefreshAsync()
    {
        var appointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Medical");
        Visits.Clear();

        foreach (var appt in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);
            var canConsult = appt.Status is "CheckedIn" or "VitalsRecorded";

            Visits.Add(new TodaysVisitItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                PatientRole = patient?.Role ?? string.Empty,
                TimeDisplay = appt.VisitTime,
                ChiefComplaint = string.IsNullOrWhiteSpace(appt.ReasonOrPurpose) ? appt.SubService : appt.ReasonOrPurpose,
                Status = appt.Status switch
                {
                    "VitalsRecorded" => "Vitals recorded",
                    "CheckedIn" => "Checked in",
                    "Completed" => "Completed",
                    "Confirmed" => "Confirmed",
                    "Cancelled" => "Cancelled",
                    "Missed" => "Missed",
                    "Expired" => "Expired",
                    _ => "Pending"
                },
                CanConsult = canConsult,
                ActionMessage = canConsult
                    ? string.Empty
                    : appt.Status is "Pending" or "Confirmed"
                        ? "Consultation opens after R.E.A.D.S. checks in the patient."
                        : "This visit is not available for consultation.",
                StatusColor = appt.Status switch
                {
                    "CheckedIn" or "VitalsRecorded" => Color.FromArgb("#087A3D"),
                    "Confirmed" => Color.FromArgb("#2C9057"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    "Cancelled" or "Expired" or "Missed" => Color.FromArgb("#E05B5B"),
                    _ => Color.FromArgb("#D9A441")
                }
            });
        }

        HasVisits = Visits.Count > 0;
    }

    [RelayCommand]
    private async Task OpenConsultation(TodaysVisitItem item)
    {
        if (item == null)
            return;

        if (!item.CanConsult)
        {
            await Shell.Current.DisplayAlert(
                "Not ready for consultation",
                "The patient must be checked in by R.E.A.D.S. before the doctor can start the visit.",
                "OK");
            return;
        }

        var appointment = await Services.AppointmentService.Instance.GetByIdAsync(item.AppointmentId);
        if (appointment == null || appointment.Status is not ("CheckedIn" or "VitalsRecorded"))
        {
            await Shell.Current.DisplayAlert(
                "Visit unavailable",
                "This visit is no longer checked in. Refresh the schedule to see the latest status.",
                "OK");
            await RefreshAsync();
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.ConsultationPage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.PatientRole, item.ChiefComplaint));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}