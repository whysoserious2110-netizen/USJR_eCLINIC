using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.ViewModels;

public partial class StaffManagementViewModel : ObservableObject
{
    private List<UserAccount> _allStaff = new();

    public ObservableCollection<UserAccount> StaffMembers { get; } = new();
    public ObservableCollection<string> RoleFilters { get; } = new(new[] { "All roles" }.Concat(Services.AuthService.ClinicStaffRoles));

    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedRoleFilter = "All roles";
    [ObservableProperty] private string resultSummary = "Loading staff…";
    [ObservableProperty] private bool isBusy;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedRoleFilterChanged(string value) => ApplyFilter();

    public StaffManagementViewModel() { }

    public Task RefreshAsync() => Load();

    [RelayCommand]
    private async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (!Services.AuthService.IsClinicAdministrator(Services.AuthService.Instance.CurrentUser))
            {
                await Shell.Current.DisplayAlert("Access denied", "Only the Clinic Administrator can manage clinic staff.", "OK");
                await Shell.Current.Navigation.PopAsync();
                return;
            }

            _allStaff = await Services.AuthService.Instance.GetClinicStaffAsync();
            ApplyFilter();
        }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        IEnumerable<UserAccount> result = _allStaff;
        if (SelectedRoleFilter != "All roles")
            result = result.Where(account => string.Equals(account.Role, SelectedRoleFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query))
            result = result.Where(account =>
                account.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                account.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                account.IdNumber.Contains(query, StringComparison.OrdinalIgnoreCase));

        var list = result.OrderBy(account => account.Role).ThenBy(account => account.FullName).ToList();
        StaffMembers.Clear();
        foreach (var staff in list)
            StaffMembers.Add(staff);
        ResultSummary = list.Count == 1 ? "1 clinic employee" : $"{list.Count} clinic employees";
    }

    [RelayCommand]
    private async Task AddStaff() => await Shell.Current.Navigation.PushAsync(new Views.StaffEditorPage());

    [RelayCommand]
    private async Task EditStaff(UserAccount? staff)
    {
        if (staff != null)
            await Shell.Current.Navigation.PushAsync(new Views.StaffEditorPage(staff));
    }

    [RelayCommand]
    private async Task ToggleActive(UserAccount? staff)
    {
        if (staff == null) return;
        var activate = !string.Equals(staff.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase);
        var action = activate ? "activate" : "deactivate";
        if (!await Shell.Current.DisplayAlert($"{char.ToUpper(action[0]) + action[1..]} employee?",
                $"{staff.FullName} will { (activate ? "be able to sign in and be assigned new visits" : "not be able to sign in or receive new appointments") }.",
                char.ToUpper(action[0]) + action[1..], "Cancel"))
            return;

        var saved = await Services.AuthService.Instance.SetClinicStaffActiveAsync(staff.Id, activate);
        await Shell.Current.DisplayAlert(saved ? "Staff updated" : "Update failed",
            saved ? $"{staff.FullName} is now {(activate ? "active" : "inactive")}." : "The account could not be updated.", "OK");
        await Load();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}

public partial class StaffEditorViewModel : ObservableObject
{
    private readonly int _staffId;
    public ObservableCollection<string> StaffRoles { get; } = new(Services.AuthService.ClinicStaffRoles);

    [ObservableProperty] private string fullName = string.Empty;
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string idNumber = string.Empty;
    [ObservableProperty] private string selectedRole = Services.AuthService.ClinicStaffRoles[0];
    [ObservableProperty] private string department = "USJ-R Clinic";
    [ObservableProperty] private string position = string.Empty;
    [ObservableProperty] private string specialization = string.Empty;
    [ObservableProperty] private string licenseNumber = string.Empty;
    [ObservableProperty] private string initialPassword = string.Empty;
    [ObservableProperty] private bool isBusy;

    public bool IsCreatingStaff => _staffId == 0;
    public string PageHeading => IsCreatingStaff ? "Add Clinic Employee" : "Edit Clinic Employee";
    public string SaveButtonText => IsCreatingStaff ? "Create Staff Account" : "Save Employee Details";

    public StaffEditorViewModel(UserAccount? staff = null)
    {
        _staffId = staff?.Id ?? 0;
        if (staff == null) return;
        FullName = staff.FullName;
        Email = staff.Email;
        IdNumber = staff.IdNumber;
        SelectedRole = StaffRoles.FirstOrDefault(role => string.Equals(role, staff.Role, StringComparison.OrdinalIgnoreCase)) ?? StaffRoles[0];
        Department = staff.ProgramOrDepartment;
        Position = staff.Position;
        Specialization = staff.Specialization;
        LicenseNumber = staff.LicenseNumber;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(IdNumber))
        {
            await Shell.Current.DisplayAlert("Missing information", "Enter the employee name, email, and employee ID.", "OK");
            return;
        }
        if (IsCreatingStaff && InitialPassword.Trim().Length < 8)
        {
            await Shell.Current.DisplayAlert("Password required", "Set an initial password with at least 8 characters.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var account = new UserAccount
            {
                Id = _staffId,
                FullName = FullName,
                Email = Email,
                IdNumber = IdNumber,
                Role = SelectedRole,
                ProgramOrDepartment = Department,
                Position = Position,
                Specialization = Specialization,
                LicenseNumber = LicenseNumber
            };

            var saved = IsCreatingStaff
                ? await Services.AuthService.Instance.CreateClinicStaffAsync(account, InitialPassword)
                : await Services.AuthService.Instance.UpdateClinicStaffAsync(account);

            await Shell.Current.DisplayAlert(saved ? "Saved" : "Could not save",
                saved ? (IsCreatingStaff ? "The staff account is active and ready to sign in." : "Employee information has been updated.")
                      : "Check for a duplicate employee ID or email, or confirm that you are signed in as the Clinic Administrator.", "OK");
            if (saved)
                await Shell.Current.Navigation.PopAsync();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}

public partial class WeekdayScheduleEditor : ObservableObject
{
    public int DayOfWeek { get; }
    public string DayName { get; }
    public ObservableCollection<string> ShiftTypes { get; } = new(Services.StaffScheduleService.ShiftTypes);
    public string HoursDisplay => $"{StartsAt:hh\\:mm}–{EndsAt:hh\\:mm}" + (EndsAt <= StartsAt ? " next day" : string.Empty);
    public string DayStateText => IsWorkingDay ? "Working" : "Day off";

    [ObservableProperty] private bool isWorkingDay;
    [ObservableProperty] private TimeSpan startsAt = new(8, 0, 0);
    [ObservableProperty] private TimeSpan endsAt = new(17, 0, 0);
    [ObservableProperty] private string shiftType = "Custom";

    public WeekdayScheduleEditor(DayOfWeek day, bool working, TimeSpan start, TimeSpan end)
    {
        DayOfWeek = (int)day;
        DayName = day.ToString();
        IsWorkingDay = working;
        StartsAt = start;
        EndsAt = end;
    }

    partial void OnShiftTypeChanged(string value)
    {
        switch (value)
        {
            case "Morning": StartsAt = new(7, 0, 0); EndsAt = new(12, 0, 0); break;
            case "Afternoon": StartsAt = new(13, 0, 0); EndsAt = new(19, 0, 0); break;
        }
        OnPropertyChanged(nameof(HoursDisplay));
    }

    partial void OnIsWorkingDayChanged(bool value) => OnPropertyChanged(nameof(DayStateText));
    partial void OnStartsAtChanged(TimeSpan value) => OnPropertyChanged(nameof(HoursDisplay));
    partial void OnEndsAtChanged(TimeSpan value) => OnPropertyChanged(nameof(HoursDisplay));
}

public sealed class RecurringScheduleHistoryEntry
{
    public int Id { get; init; }
    public bool IsActive { get; init; }
    public string EffectivePeriod { get; init; } = string.Empty;
    public string Pattern { get; init; } = string.Empty;
    public string AuditText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
}

public partial class StaffScheduleViewModel : ObservableObject
{
    private readonly UserAccount _staff;

    public ObservableCollection<WeekdayScheduleEditor> WeekDays { get; } = new();
    public ObservableCollection<string> LeaveTypes { get; } = new(Services.StaffScheduleService.LeaveTypes);
    public ObservableCollection<StaffLeave> Leaves { get; } = new();
    public ObservableCollection<RecurringScheduleHistoryEntry> RecurringHistory { get; } = new();

    [ObservableProperty] private string changeNote = string.Empty;
    [ObservableProperty] private bool isChangeNoteExpanded;
    [ObservableProperty] private DateTime leaveStartDate = DateTime.Today;
    [ObservableProperty] private DateTime leaveEndDate = DateTime.Today;
    [ObservableProperty] private string selectedLeaveType = "Vacation Leave";
    [ObservableProperty] private string leaveReason = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isHistoryExpanded;

    public string StaffName => _staff.FullName;
    public string StaffRole => _staff.Role;
    public string StaffDisplay => $"{_staff.FullName} · {_staff.Role}";
    public string ChangeNoteButtonText => IsChangeNoteExpanded ? "Hide note" : "Add a note (optional)";
    public string HistorySectionTitle => $"Weekly Schedule History ({RecurringHistory.Count}) {(IsHistoryExpanded ? "▲" : "▼")}";
    public string HistoryEmptyMessage => RecurringHistory.Count == 0 ? "No schedule history yet." : string.Empty;
    public string LeaveEmptyMessage => Leaves.Count == 0 ? "No time off has been recorded." : string.Empty;

    public StaffScheduleViewModel(UserAccount staff)
    {
        _staff = staff;
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                    DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday })
            WeekDays.Add(new WeekdayScheduleEditor(day, false, new(8, 0, 0), new(17, 0, 0)));
    }

    public Task RefreshAsync() => Load();

    partial void OnIsChangeNoteExpandedChanged(bool value) => OnPropertyChanged(nameof(ChangeNoteButtonText));
    partial void OnIsHistoryExpandedChanged(bool value) => OnPropertyChanged(nameof(HistorySectionTitle));

    [RelayCommand]
    private void ToggleChangeNote() => IsChangeNoteExpanded = !IsChangeNoteExpanded;

    [RelayCommand]
    private void ToggleHistory() => IsHistoryExpanded = !IsHistoryExpanded;

    [RelayCommand]
    private async Task CopyFirstWorkingDayToAllWorkdays()
    {
        var source = WeekDays.FirstOrDefault(day => day.IsWorkingDay);
        if (source == null)
        {
            await Shell.Current.DisplayAlert("No working day", "Choose a shift for at least one day first.", "OK");
            return;
        }

        var shiftType = source.ShiftType;
        var startsAt = source.StartsAt;
        var endsAt = source.EndsAt;
        foreach (var day in WeekDays)
        {
            day.ShiftType = shiftType;
            day.StartsAt = startsAt;
            day.EndsAt = endsAt;
            day.IsWorkingDay = true;
        }
    }

    [RelayCommand]
    private async Task Load()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var service = Services.StaffScheduleService.Instance;
            var leaveEntries = await service.GetLeaveForStaffAsync(_staff.Id);
            Leaves.Clear();
            foreach (var leave in leaveEntries) Leaves.Add(leave);

            var history = await service.GetRecurringHistoryAsync(_staff.Id);
            var historyEntries = new List<RecurringScheduleHistoryEntry>();
            foreach (var schedule in history)
            {
                var daySettings = await service.GetDaysForScheduleAsync(schedule.Id);
                var workingDays = daySettings.Where(day => day.IsWorkingDay).Select(day =>
                    $"{((DayOfWeek)day.DayOfWeek).ToString()[..3]} {day.StartTime}–{day.EndTime}").ToList();
                var restDays = daySettings.Where(day => !day.IsWorkingDay)
                    .Select(day => ((DayOfWeek)day.DayOfWeek).ToString()[..3] + " rest").ToList();
                var endText = schedule.EffectiveEndDate.HasValue ? schedule.EffectiveEndDate.Value.ToString("MMM d, yyyy") : "No end date";
                var status = schedule.IsActive ? "Current" : "Replaced";
                historyEntries.Add(new RecurringScheduleHistoryEntry
                {
                    Id = schedule.Id,
                    IsActive = schedule.IsActive,
                    EffectivePeriod = $"{schedule.EffectiveStartDate:MMM d, yyyy} – {endText}",
                    Pattern = workingDays.Count == 0 ? "No working days" : string.Join(" · ", workingDays.Concat(restDays)),
                    AuditText = $"Saved by {schedule.CreatedByName} on {schedule.CreatedAt:MMM d, yyyy}" +
                                (string.IsNullOrWhiteSpace(schedule.ChangeNote) ? string.Empty : $" · {schedule.ChangeNote}") +
                                (schedule.ModifiedAt.HasValue ? $" · Changed by {schedule.ModifiedByName} on {schedule.ModifiedAt:MMM d, yyyy}" : string.Empty),
                    StatusText = status
                });
            }
            RecurringHistory.Clear();
            foreach (var entry in historyEntries) RecurringHistory.Add(entry);
            OnPropertyChanged(nameof(HistoryEmptyMessage));
            OnPropertyChanged(nameof(HistorySectionTitle));

            var selected = history.FirstOrDefault(item => item.IsActive && item.EffectiveStartDate.Date <= DateTime.Today &&
                                                          (!item.EffectiveEndDate.HasValue || item.EffectiveEndDate.Value.Date >= DateTime.Today))
                           ?? history.FirstOrDefault(item => item.IsActive && item.EffectiveStartDate.Date > DateTime.Today);

            foreach (var editor in WeekDays)
            {
                editor.IsWorkingDay = false;
                editor.ShiftType = "Custom";
                editor.StartsAt = new(8, 0, 0);
                editor.EndsAt = new(17, 0, 0);
            }

            if (selected != null)
            {
                var savedDays = await service.GetDaysForScheduleAsync(selected.Id);
                foreach (var editor in WeekDays)
                {
                    var savedDay = savedDays.FirstOrDefault(day => day.DayOfWeek == editor.DayOfWeek);
                    if (savedDay == null) continue;
                    editor.IsWorkingDay = savedDay.IsWorkingDay;
                    editor.ShiftType = savedDay.ShiftType == "Night" ? "Custom" : savedDay.ShiftType;
                    if (TimeSpan.TryParse(savedDay.StartTime, out var start)) editor.StartsAt = start;
                    if (TimeSpan.TryParse(savedDay.EndTime, out var end)) editor.EndsAt = end;
                }
            }
            OnPropertyChanged(nameof(LeaveEmptyMessage));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveRecurring()
    {
        if (!WeekDays.Any(day => day.IsWorkingDay))
        {
            await Shell.Current.DisplayAlert("Choose workdays", "Turn on at least one day before saving the weekly schedule.", "OK");
            return;
        }
        if (WeekDays.Any(day => day.IsWorkingDay && day.StartsAt == day.EndsAt))
        {
            await Shell.Current.DisplayAlert("Check shift hours", "Each working day needs different start and end times.", "OK");
            return;
        }

        var rows = WeekDays.Select(day => new StaffRecurringScheduleDay
        {
            StaffId = _staff.Id,
            DayOfWeek = day.DayOfWeek,
            IsWorkingDay = day.IsWorkingDay,
            StartTime = day.StartsAt.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture),
            EndTime = day.EndsAt.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture),
            ShiftType = day.ShiftType
        }).ToList();
        rows.Add(new StaffRecurringScheduleDay
        {
            StaffId = _staff.Id,
            DayOfWeek = (int)DayOfWeek.Sunday,
            IsWorkingDay = false,
            StartTime = "00:00",
            EndTime = "00:00",
            ShiftType = "Custom"
        });
        var saved = await Services.StaffScheduleService.Instance.SaveRecurringScheduleAsync(
            _staff.Id, DateTime.Today, null, rows, ChangeNote);
        await Shell.Current.DisplayAlert(saved ? "Weekly schedule saved" : "Schedule not saved",
            saved ? "The clinic will use this weekly schedule when checking appointment availability." :
                    "Keep morning shifts within 7:00 AM–12:00 PM, afternoon shifts within 1:00–7:00 PM, and custom hours within clinic hours. A shift cannot start or end during the 12:00–1:00 PM break, and working days cannot overlap.", "OK");
        if (saved)
        {
            ChangeNote = string.Empty;
            IsChangeNoteExpanded = false;
            await Load();
        }
    }

    [RelayCommand]
    private async Task AddLeave()
    {
        var saved = await Services.StaffScheduleService.Instance.AddLeaveAsync(new StaffLeave
        {
            StaffId = _staff.Id,
            StartDate = LeaveStartDate.Date,
            EndDate = LeaveEndDate.Date,
            LeaveType = SelectedLeaveType,
            Reason = LeaveReason.Trim()
        });
        await Shell.Current.DisplayAlert(saved ? "Time off saved" : "Could not save time off",
            saved ? "The employee will not receive new appointment assignments during this period." :
                "Check the dates and type. Time off periods for the same employee cannot overlap.", "OK");
        if (saved)
        {
            LeaveReason = string.Empty;
            await Load();
        }
    }

    [RelayCommand]
    private async Task CancelLeave(StaffLeave? leave)
    {
        if (leave == null || leave.Status != "Approved" ||
            !await Shell.Current.DisplayAlert("Cancel this time off?", $"{leave.LeaveType}: {leave.StartDate:MMM d}–{leave.EndDate:MMM d, yyyy}", "Cancel time off", "Keep")) return;
        if (await Services.StaffScheduleService.Instance.CancelLeaveAsync(leave.Id)) await Load();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
