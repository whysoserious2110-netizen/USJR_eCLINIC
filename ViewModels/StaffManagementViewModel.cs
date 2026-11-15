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
    private async Task ManageSchedule(UserAccount? staff)
    {
        if (staff != null)
            await Shell.Current.Navigation.PushAsync(new Views.StaffSchedulePage(staff));
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
            case "Morning": StartsAt = new(7, 0, 0); EndsAt = new(15, 0, 0); break;
            case "Afternoon": StartsAt = new(13, 0, 0); EndsAt = new(21, 0, 0); break;
            case "Night": StartsAt = new(22, 0, 0); EndsAt = new(6, 0, 0); break;
        }
        OnPropertyChanged(nameof(HoursDisplay));
    }

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
    private bool _daysInitialized;

    public ObservableCollection<WeekdayScheduleEditor> WeekDays { get; } = new();
    public ObservableCollection<string> DayNames { get; } = new(new[]
        { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" });
    public ObservableCollection<WeekdayScheduleEditor> ConfiguredDays { get; } = new();
    public ObservableCollection<string> ShiftTypes { get; } = new(Services.StaffScheduleService.ShiftTypes);
    public ObservableCollection<string> LeaveTypes { get; } = new(Services.StaffScheduleService.LeaveTypes);
    public ObservableCollection<StaffLeave> Leaves { get; } = new();
    public ObservableCollection<RecurringScheduleHistoryEntry> RecurringHistory { get; } = new();

    [ObservableProperty] private DateTime effectiveStartDate = DateTime.Today;
    [ObservableProperty] private DateTime effectiveEndDate = DateTime.Today;
    [ObservableProperty] private bool hasEndDate;
    [ObservableProperty] private string changeNote = string.Empty;
    [ObservableProperty] private string selectedDayName = "Monday";
    [ObservableProperty] private string selectedShiftType = "Custom";
    [ObservableProperty] private TimeSpan selectedDayStartsAt = new(8, 0, 0);
    [ObservableProperty] private TimeSpan selectedDayEndsAt = new(17, 0, 0);
    [ObservableProperty] private string dayActionText = "Add Working Day";
    [ObservableProperty] private DateTime leaveStartDate = DateTime.Today;
    [ObservableProperty] private DateTime leaveEndDate = DateTime.Today;
    [ObservableProperty] private string selectedLeaveType = "Unavailable";
    [ObservableProperty] private string leaveReason = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string historyEmptyMessage = string.Empty;
    [ObservableProperty] private bool isScheduleEditorExpanded;
    [ObservableProperty] private bool isHistoryExpanded;

    public string StaffName => _staff.FullName;
    public string StaffRole => _staff.Role;
    public string StaffDisplay => $"{_staff.FullName} · {_staff.Role}";
    public string ScheduleEditorButtonText => IsScheduleEditorExpanded
        ? "Close Schedule Editor"
        : RecurringHistory.Any(item => item.IsActive) ? "Update Selected Schedule" : "Set Weekly Schedule";
    public string HistorySectionTitle => $"Weekly Schedule History ({RecurringHistory.Count}) {(IsHistoryExpanded ? "▲" : "▼")}";
    public string WorkingDaysEmptyMessage => ConfiguredDays.Count == 0 ? "No working days selected yet. Select a day, choose its shift and hours, then add it." : string.Empty;
    public string LeaveEmptyMessage => Leaves.Count == 0 ? "No leave or unavailable periods have been recorded." : string.Empty;

    public StaffScheduleViewModel(UserAccount staff)
    {
        _staff = staff;
        InitializeWeekDays();
    }

    public Task RefreshAsync() => Load();

    partial void OnSelectedDayNameChanged(string value) => LoadSelectedDayIntoEditor();

    partial void OnIsScheduleEditorExpandedChanged(bool value) => OnPropertyChanged(nameof(ScheduleEditorButtonText));
    partial void OnIsHistoryExpandedChanged(bool value) => OnPropertyChanged(nameof(HistorySectionTitle));

    [RelayCommand]
    private void ToggleScheduleEditor() => IsScheduleEditorExpanded = !IsScheduleEditorExpanded;

    [RelayCommand]
    private void ToggleHistory() => IsHistoryExpanded = !IsHistoryExpanded;

    partial void OnSelectedShiftTypeChanged(string value)
    {
        switch (value)
        {
            case "Morning": SelectedDayStartsAt = new(7, 0, 0); SelectedDayEndsAt = new(15, 0, 0); break;
            case "Afternoon": SelectedDayStartsAt = new(13, 0, 0); SelectedDayEndsAt = new(21, 0, 0); break;
            case "Night": SelectedDayStartsAt = new(22, 0, 0); SelectedDayEndsAt = new(6, 0, 0); break;
        }
    }

    private void InitializeWeekDays()
    {
        if (_daysInitialized) return;
        _daysInitialized = true;
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                                   DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
        {
            WeekDays.Add(new WeekdayScheduleEditor(day, false, new(8, 0, 0), new(17, 0, 0)));
        }
        RefreshConfiguredDays();
        LoadSelectedDayIntoEditor();
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
                var status = !schedule.IsActive ? "Inactive / superseded" :
                    schedule.EffectiveStartDate.Date > DateTime.Today ? "Scheduled" : "Active";
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
            HistoryEmptyMessage = RecurringHistory.Count == 0 ? "No weekly schedule history yet. Save a recurring schedule once to configure future weeks." : string.Empty;
            OnPropertyChanged(nameof(ScheduleEditorButtonText));
            OnPropertyChanged(nameof(HistorySectionTitle));

            var selected = history.FirstOrDefault(item => item.IsActive && item.EffectiveStartDate.Date <= DateTime.Today &&
                                                          (!item.EffectiveEndDate.HasValue || item.EffectiveEndDate.Value.Date >= DateTime.Today))
                           ?? history.FirstOrDefault(item => item.IsActive && item.EffectiveStartDate.Date > DateTime.Today);
            if (selected != null)
            {
                EffectiveStartDate = selected.EffectiveStartDate.Date < DateTime.Today ? DateTime.Today : selected.EffectiveStartDate.Date;
                HasEndDate = selected.EffectiveEndDate.HasValue;
                if (selected.EffectiveEndDate.HasValue) EffectiveEndDate = selected.EffectiveEndDate.Value.Date;
                var savedDays = await service.GetDaysForScheduleAsync(selected.Id);
                foreach (var editor in WeekDays)
                {
                    var savedDay = savedDays.FirstOrDefault(day => day.DayOfWeek == editor.DayOfWeek);
                    if (savedDay == null) continue;
                    editor.IsWorkingDay = editor.DayOfWeek != (int)DayOfWeek.Sunday && savedDay.IsWorkingDay;
                    editor.ShiftType = savedDay.ShiftType;
                    if (TimeSpan.TryParse(savedDay.StartTime, out var start)) editor.StartsAt = start;
                    if (TimeSpan.TryParse(savedDay.EndTime, out var end)) editor.EndsAt = end;
                }
            }
            RefreshConfiguredDays();
            LoadSelectedDayIntoEditor();
            OnPropertyChanged(nameof(LeaveEmptyMessage));
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveRecurring()
    {
        if (ConfiguredDays.Count == 0)
        {
            await Shell.Current.DisplayAlert("Choose workdays", "Select at least one working day, or leave this employee without an active weekly schedule.", "OK");
            return;
        }
        if (ConfiguredDays.Any(day => day.StartsAt == day.EndsAt))
        {
            await Shell.Current.DisplayAlert("Check shift hours", "A working day must have different start and end times. Night shifts may end after midnight.", "OK");
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
        var saved = await Services.StaffScheduleService.Instance.SaveRecurringScheduleAsync(
            _staff.Id, EffectiveStartDate, HasEndDate ? EffectiveEndDate : null, rows, ChangeNote);
        await Shell.Current.DisplayAlert(saved ? "Weekly schedule saved" : "Schedule not saved",
            saved ? "This weekly pattern will be applied automatically to future appointment dates. The previous version remains in schedule history." :
                    "Check the effective dates and each selected shift. The end date must not be earlier than the start date.", "OK");
        if (saved)
        {
            ChangeNote = string.Empty;
            IsScheduleEditorExpanded = false;
            await Load();
        }
    }

    [RelayCommand]
    private async Task AddOrUpdateWorkDay()
    {
        if (SelectedDayStartsAt == SelectedDayEndsAt)
        {
            await Shell.Current.DisplayAlert("Check shift hours", "Choose different start and end times. Night shifts may end after midnight.", "OK");
            return;
        }
        var selected = WeekDays.First(day => day.DayName == SelectedDayName);
        selected.IsWorkingDay = true;
        selected.ShiftType = SelectedShiftType;
        selected.StartsAt = SelectedDayStartsAt;
        selected.EndsAt = SelectedDayEndsAt;
        RefreshConfiguredDays();
        DayActionText = "Update Selected Day";
    }

    [RelayCommand]
    private void EditWorkDay(WeekdayScheduleEditor? day)
    {
        if (day == null) return;
        SelectedDayName = day.DayName;
        LoadSelectedDayIntoEditor();
    }

    [RelayCommand]
    private void RemoveWorkDay(WeekdayScheduleEditor? day)
    {
        if (day == null) return;
        day.IsWorkingDay = false;
        RefreshConfiguredDays();
        if (day.DayName == SelectedDayName) LoadSelectedDayIntoEditor();
    }

    private void LoadSelectedDayIntoEditor()
    {
        var selected = WeekDays.FirstOrDefault(day => day.DayName == SelectedDayName);
        if (selected == null) return;
        if (selected.IsWorkingDay)
        {
            SelectedShiftType = selected.ShiftType;
            SelectedDayStartsAt = selected.StartsAt;
            SelectedDayEndsAt = selected.EndsAt;
            DayActionText = "Update Selected Day";
        }
        else
        {
            SelectedShiftType = "Custom";
            SelectedDayStartsAt = new(8, 0, 0);
            SelectedDayEndsAt = new(17, 0, 0);
            DayActionText = "Add Working Day";
        }
    }

    private void RefreshConfiguredDays()
    {
        ConfiguredDays.Clear();
        foreach (var day in WeekDays.Where(item => item.IsWorkingDay)) ConfiguredDays.Add(day);
        OnPropertyChanged(nameof(WorkingDaysEmptyMessage));
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
        await Shell.Current.DisplayAlert(saved ? "Unavailable period saved" : "Could not save period",
            saved ? "The employee will not receive new appointment assignments during this approved period." :
                "Check the date range and leave type. Approved periods for the same employee cannot overlap.", "OK");
        if (saved)
        {
            LeaveReason = string.Empty;
            await Load();
        }
    }

    [RelayCommand]
    private async Task DeactivateRecurring(RecurringScheduleHistoryEntry? schedule)
    {
        if (schedule == null || !schedule.IsActive ||
            !await Shell.Current.DisplayAlert("Deactivate this weekly version?", "It will remain in history and will no longer be used for appointment availability.", "Deactivate", "Cancel")) return;
        if (await Services.StaffScheduleService.Instance.DeactivateRecurringScheduleAsync(schedule.Id)) await Load();
    }

    [RelayCommand]
    private async Task CancelLeave(StaffLeave? leave)
    {
        if (leave == null || leave.Status != "Approved" ||
            !await Shell.Current.DisplayAlert("Cancel this unavailable period?", $"{leave.LeaveType}: {leave.StartDate:MMM d}–{leave.EndDate:MMM d, yyyy}", "Cancel period", "Keep")) return;
        if (await Services.StaffScheduleService.Instance.CancelLeaveAsync(leave.Id)) await Load();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.Navigation.PopAsync();
}
