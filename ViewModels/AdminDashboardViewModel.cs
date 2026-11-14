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

public partial class AdminDashboardViewModel : ObservableObject
{
    [ObservableProperty] private string adminName = "Clinic Administrator";
    [ObservableProperty] private bool isBusy;
    public ObservableCollection<StaffIdReviewItem> PendingStaffIdAccounts { get; } = new();
    public string PendingCountText => $"{PendingStaffIdAccounts.Count} pending staff account(s)";

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
            var accounts = await Services.AuthService.Instance.GetPendingStaffIdAccountsAsync();
            PendingStaffIdAccounts.Clear();
            foreach (var account in accounts)
                PendingStaffIdAccounts.Add(new StaffIdReviewItem
                {
                    Id = account.Id,
                    Role = account.Role,
                    FullName = account.FullName,
                    IdNumber = account.IdNumber,
                    Email = account.Email,
                    ProgramOrDepartment = account.ProgramOrDepartment,
                    FacultyIdImagePath = account.FacultyIdImagePath
                });
            OnPropertyChanged(nameof(PendingCountText));
        }
        finally { IsBusy = false; }
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
            success ? "The Faculty account can now sign in." : "The account could not be approved. Check the ID photo and reviewer access.", "OK");
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

    [RelayCommand]
    private async Task Logout()
    {
        await Services.SecureSessionService.Instance.ClearSessionAsync();
        Services.AuthService.Instance.Logout();
        await Shell.Current.Navigation.PopToRootAsync();
    }

    [RelayCommand]
    private async Task ChangePassword() => await Shell.Current.Navigation.PushAsync(new Views.ChangePasswordPage());

    private async Task LoadAsync() => await Load();
}
