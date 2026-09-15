using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class NurseAppointmentItem : ObservableObject
{
    public string PatientName { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string SubService { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
}

public partial class NurseAppointmentsViewModel : ObservableObject
{
    public ObservableCollection<NurseAppointmentItem> TodaysAppointments { get; } = new();

    [ObservableProperty]
    private bool hasAppointments;

    public async Task RefreshAsync()
    {
        var appointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Medical");

        TodaysAppointments.Clear();
        foreach (var appt in appointments.Where(a => a.Status != "Cancelled"))
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            TodaysAppointments.Add(new NurseAppointmentItem
            {
                PatientName = patient?.FullName ?? appt.PatientEmail,
                TimeDisplay = appt.VisitTime,
                ServiceType = appt.ServiceType,
                SubService = string.IsNullOrWhiteSpace(appt.ReasonOrPurpose) ? appt.SubService : appt.ReasonOrPurpose,
                Status = appt.Status,
                StatusColor = appt.Status switch
                {
                    "Confirmed" => Color.FromArgb("#0F9B8E"),
                    "CheckedIn" => Color.FromArgb("#2E6FDB"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    _ => Color.FromArgb("#D9A441")
                }
            });
        }

        HasAppointments = TodaysAppointments.Count > 0;
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}