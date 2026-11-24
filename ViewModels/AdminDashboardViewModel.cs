using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.ViewModels;

public sealed class StaffIdReviewItem
{
    public int Id { get; init; }
    public string Role { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string IdNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ProgramOrDepartment { get; init; } = string.Empty;
    public string FacultyIdImagePath { get; init; } = string.Empty;
}

public sealed class ClinicStaffScheduleDisplay
{
    public UserAccount Staff { get; init; } = new();
    public string FullName => Staff.FullName;
    public string Role => Staff.Role;
    public string ScheduleText { get; init; } = string.Empty;
    public bool IsScheduled { get; init; }
}

public partial class AdminDashboardViewModel : ObservableObject
{
    [ObservableProperty] private string adminName = "Clinic Administrator";
    [ObservableProperty] private bool isBusy;

    public ObservableCollection<StaffIdReviewItem> PendingStaffIdAccounts { get; } = new();
    public ObservableCollection<StaffIdReviewItem> PendingApprovalPreview { get; } = new();
    public ObservableCollection<ClinicStaffScheduleDisplay> StaffOnDutyPreview { get; } = new();
    public ObservableCollection<ClinicStaffScheduleDisplay> StaffScheduleOverview { get; } = new();

    [ObservableProperty] private string pendingApprovalsCount = "0";
    [ObservableProperty] private string activeStaffCount = "0";
    [ObservableProperty] private string staffOnDutyCount = "0";
    [ObservableProperty] private string approvalPreviewEmptyMessage = "No Faculty or Non-Teaching accounts need review.";
    [ObservableProperty] private string staffPreviewEmptyMessage = "No clinic staff are scheduled to work today.";

    public string CurrentDateText => DateTime.Now.ToString("dddd, MMMM d, yyyy");
    public string StaffSchedulesDateText => DateTime.Now.ToString("dddd, MMMM d, yyyy");
    public bool HasPendingApprovals => PendingApprovalPreview.Count > 0;
    public bool HasStaffOnDuty => StaffOnDutyPreview.Count > 0;
    public bool HasStaffScheduleOverview => StaffScheduleOverview.Count > 0;
    public bool IsApprovalPreviewEmpty => !HasPendingApprovals;
    public bool IsStaffPreviewEmpty => !HasStaffOnDuty;

    public AdminDashboardViewModel()
    {
        adminName = Services.AuthService.Instance.CurrentUser?.FullName ?? adminName;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            AdminName = Services.AuthService.Instance.CurrentUser?.FullName ?? "Clinic Administrator";
            var pendingAccounts = await Services.AuthService.Instance.GetPendingStaffIdAccountsAsync();
            var activeStaff = await Services.AuthService.Instance.GetClinicStaffAsync(includeInactive: false);
            var scheduleOverview = await Services.StaffScheduleService.Instance
                .GetStaffScheduleOverviewForDateAsync(DateTime.Today);

            PendingStaffIdAccounts.Clear();
            foreach (var account in pendingAccounts)
                PendingStaffIdAccounts.Add(ToReviewItem(account));

            PendingApprovalPreview.Clear();
            foreach (var item in PendingStaffIdAccounts.Take(3))
                PendingApprovalPreview.Add(item);

            StaffScheduleOverview.Clear();
            foreach (var item in scheduleOverview.Select(item => new ClinicStaffScheduleDisplay
                     {
                         Staff = item.Staff,
                         ScheduleText = item.ScheduleText,
                         IsScheduled = item.IsScheduled
                     }))
                StaffScheduleOverview.Add(item);

            StaffOnDutyPreview.Clear();
            foreach (var item in StaffScheduleOverview.Where(item => item.IsScheduled).Take(4))
                StaffOnDutyPreview.Add(item);

            PendingApprovalsCount = pendingAccounts.Count.ToString();
            ActiveStaffCount = activeStaff.Count.ToString();
            StaffOnDutyCount = StaffScheduleOverview.Count(item => item.IsScheduled).ToString();
            OnPropertyChanged(nameof(CurrentDateText));
            OnPropertyChanged(nameof(StaffSchedulesDateText));
            OnPropertyChanged(nameof(HasPendingApprovals));
            OnPropertyChanged(nameof(HasStaffOnDuty));
            OnPropertyChanged(nameof(HasStaffScheduleOverview));
            OnPropertyChanged(nameof(IsApprovalPreviewEmpty));
            OnPropertyChanged(nameof(IsStaffPreviewEmpty));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Dashboard unavailable",
                $"The clinic overview could not be loaded. {ex.Message}", "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OpenAccountApprovals() =>
        await Shell.Current.Navigation.PushAsync(new Views.AccountApprovalsPage());

    [RelayCommand]
    private async Task OpenStaffManagement() =>
        await Shell.Current.Navigation.PushAsync(new Views.StaffManagementPage());

    [RelayCommand]
    private async Task OpenStaffSchedules() =>
        await Shell.Current.Navigation.PushAsync(new Views.StaffSchedulesPage());

    [RelayCommand]
    private async Task OpenSettings() =>
        await Shell.Current.Navigation.PushAsync(new Views.ChangePasswordPage());

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

    [RelayCommand]
    private async Task Logout()
    {
        await Services.SecureSessionService.Instance.SignOutAsync();
        await Shell.Current.Navigation.PopToRootAsync();
    }

    [RelayCommand]
    private async Task Approve(StaffIdReviewItem item)
    {
        if (item == null) return;
        var confirm = await Shell.Current.DisplayAlert("Approve staff account?",
            $"Confirm that {item.FullName}'s submitted Employee ID matches an official record.", "Approve", "Cancel");
        if (!confirm) return;
        var success = await Services.AuthService.Instance.ReviewStaffIdAccountAsync(item.Id, true);
        await Shell.Current.DisplayAlert(success ? "Account activated" : "Review unavailable",
            success ? $"{item.FullName}'s account can now sign in." : "The account could not be approved. Check the ID photo and reviewer access.", "OK");
        await Load();
    }

    [RelayCommand]
    private async Task Reject(StaffIdReviewItem item)
    {
        if (item == null) return;
        var confirm = await Shell.Current.DisplayAlert("Reject staff account?",
            "The account will remain unable to sign in. Confirm you have checked the submitted ID.", "Reject", "Cancel");
        if (!confirm) return;
        await Services.AuthService.Instance.ReviewStaffIdAccountAsync(item.Id, false);
        await Load();
    }

    private async Task LoadAsync() => await Load();

    private static StaffIdReviewItem ToReviewItem(UserAccount account) => new()
    {
        Id = account.Id,
        Role = account.Role,
        FullName = account.FullName,
        IdNumber = account.IdNumber,
        Email = account.Email,
        ProgramOrDepartment = account.ProgramOrDepartment,
        FacultyIdImagePath = account.FacultyIdImagePath
    };
}

public partial class StaffSchedulesViewModel : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string dateText = DateTime.Now.ToString("dddd, MMMM d, yyyy");
    public ObservableCollection<ClinicStaffScheduleDisplay> StaffSchedules { get; } = new();
    public string EmptyMessage => StaffSchedules.Count == 0
        ? "No active clinic staff accounts are available."
        : string.Empty;

    public StaffSchedulesViewModel() { }

    public Task RefreshAsync() => Load();

    [RelayCommand]
    private async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var scheduleOverview = await Services.StaffScheduleService.Instance
                .GetStaffScheduleOverviewForDateAsync(DateTime.Today);
            StaffSchedules.Clear();
            foreach (var item in scheduleOverview)
                StaffSchedules.Add(new ClinicStaffScheduleDisplay
                {
                    Staff = item.Staff,
                    ScheduleText = item.ScheduleText,
                    IsScheduled = item.IsScheduled
                });
            DateText = DateTime.Now.ToString("dddd, MMMM d, yyyy");
            OnPropertyChanged(nameof(EmptyMessage));
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Schedules unavailable", ex.Message, "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ManageSchedule(ClinicStaffScheduleDisplay? item)
    {
        if (item?.Staff != null)
            await Shell.Current.Navigation.PushAsync(new Views.StaffSchedulePage(item.Staff));
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();

}
