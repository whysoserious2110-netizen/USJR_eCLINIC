using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace USJR_eCLINIC.ViewModels;

public partial class PendingApprovalItem : ObservableObject
{
    public int AppointmentId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string SubService { get; set; } = string.Empty;
    public string DateDisplay { get; set; } = string.Empty;
    public string TimeDisplay { get; set; } = string.Empty;
}

public partial class ManageAppointmentsViewModel : ObservableObject
{
    public ObservableCollection<PendingApprovalItem> PendingApprovals { get; } = new();

    [ObservableProperty]
    private bool hasPending;

    public async Task RefreshAsync()
    {
        var pending = await Services.AppointmentService.Instance.GetPendingApprovalsAsync();

        PendingApprovals.Clear();
        foreach (var appt in pending)
        {
            var patient = await Services.AuthService.Instance.GetAccountByEmailAsync(appt.PatientEmail);

            PendingApprovals.Add(new PendingApprovalItem
            {
                AppointmentId = appt.Id,
                PatientName = patient?.FullName ?? appt.PatientEmail,
                ServiceType = appt.ServiceType,
                SubService = appt.SubService,
                DateDisplay = appt.VisitDate.ToString("MMM dd, yyyy"),
                TimeDisplay = appt.VisitTime
            });
        }

        HasPending = PendingApprovals.Count > 0;
    }

    [RelayCommand]
    private async Task Approve(PendingApprovalItem item)
    {
        bool confirm = await Shell.Current.DisplayAlert("Approve Appointment?", $"Approve {item.PatientName}'s {item.ServiceType} appointment?", "Approve", "Cancel");
        if (!confirm) return;

        await Services.AppointmentService.Instance.ApproveAsync(item.AppointmentId);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}