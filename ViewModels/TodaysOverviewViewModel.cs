using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class TodaysOverviewViewModel : ObservableObject
{
    [ObservableProperty]
    private string todayDisplay = string.Empty;

    [ObservableProperty]
    private int pendingApprovalCount;

    [ObservableProperty]
    private int expectedTodayCount;

    [ObservableProperty]
    private int checkedInTodayCount;

    [ObservableProperty]
    private int completedTodayCount;

    public async Task RefreshAsync()
    {
        TodayDisplay = DateTime.Today.ToString(
            "dddd, MMMM dd, yyyy");

        PendingApprovalCount =
            await Services.AppointmentService.Instance
                .GetPendingApprovalCountAsync();

        var allAppointments =
            await Services.AppointmentService.Instance
                .GetAllAppointmentsAsync();

        var todaysAppointments = allAppointments
            .Where(a => a.VisitDate.Date == DateTime.Today)
            .ToList();

        ExpectedTodayCount = todaysAppointments.Count(a =>
            a.Status == "Confirmed" ||
            a.Status == "CheckedIn" ||
            a.Status == "VitalsRecorded" ||
            a.Status == "Completed");

        CheckedInTodayCount = todaysAppointments.Count(a =>
            a.Status == "CheckedIn" ||
            a.Status == "VitalsRecorded");

        CompletedTodayCount = todaysAppointments.Count(a =>
            a.Status == "Completed");
    }

    [RelayCommand]
    private async Task GoToAppointments()
    {
        await Shell.Current.Navigation.PushAsync(
            new Views.ReadsAppointmentsOverviewPage());
    }

    [RelayCommand]
    private async Task GoBack()
    {
        await Shell.Current.Navigation.PopAsync();
    }
}