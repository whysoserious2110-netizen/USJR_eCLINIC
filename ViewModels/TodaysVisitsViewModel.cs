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
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool CanConsult { get; set; }
}

public partial class TodaysVisitsViewModel : ObservableObject
{
    public ObservableCollection<TodaysVisitItem> Visits { get; } = new();

    [ObservableProperty]
    private bool hasVisits;

    public async Task RefreshAsync()
    {
        var appointments = await Services.AppointmentService.Instance.GetTodayForServiceAsync("Medical");

        Visits.Clear();

        foreach (var appt in appointments)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            Visits.Add(new TodaysVisitItem
            {
                AppointmentId = appt.Id,
                PatientEmail = appt.PatientEmail,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                PatientRole = patient?.Role ?? string.Empty,
                TimeDisplay = appt.VisitTime,
                ChiefComplaint = string.IsNullOrWhiteSpace(appt.ReasonOrPurpose) ? appt.SubService : appt.ReasonOrPurpose,
                Status = appt.Status,
                CanConsult = appt.Status == "Pending" || appt.Status == "Confirmed",
                StatusColor = appt.Status switch
                {
                    "Confirmed" => Color.FromArgb("#0F9B8E"),
                    "Completed" => Color.FromArgb("#4A90D9"),
                    "Cancelled" => Color.FromArgb("#E05B5B"),
                    _ => Color.FromArgb("#D9A441") // Pending
                }
            });
        }

        HasVisits = Visits.Count > 0;
    }

    [RelayCommand]
    private async Task OpenConsultation(TodaysVisitItem item)
    {
        if (!item.CanConsult)
        {
            await Shell.Current.DisplayAlert("Not available", $"This appointment is already {item.Status}.", "OK");
            return;
        }

        await Shell.Current.Navigation.PushAsync(new Views.ConsultationPage(
            item.AppointmentId, item.PatientEmail, item.PatientName, item.PatientRole, item.ChiefComplaint));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}