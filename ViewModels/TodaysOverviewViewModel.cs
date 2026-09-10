using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace USJR_eCLINIC.ViewModels;

public partial class TodaysOverviewViewModel : ObservableObject
{
    [ObservableProperty] private int totalAppointments;
    [ObservableProperty] private int checkedInCount;
    [ObservableProperty] private int pendingApprovalCount;

    public async Task RefreshAsync()
    {
        TotalAppointments = await Services.AppointmentService.Instance.GetTodayTotalCountAsync();
        CheckedInCount = await Services.AppointmentService.Instance.GetTodayCheckedInCountAsync();
        PendingApprovalCount = await Services.AppointmentService.Instance.GetPendingApprovalCountAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}