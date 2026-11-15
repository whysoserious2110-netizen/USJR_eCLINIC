using System.Globalization;
using SQLite;
using USJR_eCLINIC.Models;

namespace USJR_eCLINIC.Services;

/// <summary>
/// Staff scheduling for the current device-local SQLite architecture. Recurring schedules
/// are versioned; dated overrides, additional shifts, and leave are kept as separate records.
/// </summary>
public sealed class StaffScheduleService
{
    public static StaffScheduleService Instance { get; } = new();

    public static readonly string[] ShiftTypes = { "Morning", "Afternoon", "Night", "Custom" };
    public static readonly string[] DateScheduleTypes = { "Override weekly schedule", "Additional shift" };
    public static readonly string[] DateScheduleStatuses = { "Available", "Unavailable", "Rest Day" };
    public static readonly string[] LeaveTypes = { "Vacation Leave", "Sick Leave", "Absent", "Unavailable" };

    private readonly SQLiteAsyncConnection _db = ClinicDatabase.Connection;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private StaffScheduleService() { }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            await _db.CreateTableAsync<StaffShift>();
            await _db.CreateTableAsync<StaffRecurringSchedule>();
            await _db.CreateTableAsync<StaffRecurringScheduleDay>();
            await _db.CreateTableAsync<StaffLeave>();

            // Preserve dated entries created by earlier app versions.
            var columns = await _db.QueryAsync<AppointmentColumnInfo>("PRAGMA table_info('StaffShift');");
            var existing = new HashSet<string>(columns.Select(column => column.Name), StringComparer.OrdinalIgnoreCase);
            if (!existing.Contains("ScheduleType"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN ScheduleType TEXT NOT NULL DEFAULT 'Override'");
            if (!existing.Contains("IsActive"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1");
            if (!existing.Contains("CreatedAt"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN CreatedAt DATETIME NOT NULL DEFAULT '2000-01-01'");
            if (!existing.Contains("CreatedByStaffId"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN CreatedByStaffId INTEGER NOT NULL DEFAULT 0");
            if (!existing.Contains("CreatedByName"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN CreatedByName TEXT NOT NULL DEFAULT ''");
            if (!existing.Contains("ModifiedAt"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN ModifiedAt DATETIME NULL");
            if (!existing.Contains("ModifiedByStaffId"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN ModifiedByStaffId INTEGER NULL");
            if (!existing.Contains("ModifiedByName"))
                await _db.ExecuteAsync("ALTER TABLE StaffShift ADD COLUMN ModifiedByName TEXT NOT NULL DEFAULT ''");

            _initialized = true;
        }
        finally { _initLock.Release(); }
    }

    public async Task<List<StaffShift>> GetForStaffAsync(int staffId)
    {
        await EnsureInitializedAsync();
        if (!CanManageStaff(staffId)) return new();
        return (await _db.Table<StaffShift>().Where(item => item.StaffId == staffId).ToListAsync())
            .OrderByDescending(item => item.ShiftDate).ThenBy(item => item.StartsAt).ToList();
    }

    public async Task<List<StaffLeave>> GetLeaveForStaffAsync(int staffId)
    {
        await EnsureInitializedAsync();
        if (!CanManageStaff(staffId)) return new();
        return (await _db.Table<StaffLeave>().Where(item => item.StaffId == staffId).ToListAsync())
            .OrderByDescending(item => item.StartDate).ToList();
    }

    public async Task<List<StaffRecurringSchedule>> GetRecurringHistoryAsync(int staffId)
    {
        await EnsureInitializedAsync();
        if (!CanManageStaff(staffId)) return new();
        return (await _db.Table<StaffRecurringSchedule>().Where(item => item.StaffId == staffId).ToListAsync())
            .OrderByDescending(item => item.EffectiveStartDate).ToList();
    }

    public async Task<List<StaffRecurringScheduleDay>> GetDaysForScheduleAsync(int scheduleId)
    {
        await EnsureInitializedAsync();
        var schedule = await _db.Table<StaffRecurringSchedule>()
            .Where(item => item.Id == scheduleId).FirstOrDefaultAsync();
        if (schedule == null || !CanManageStaff(schedule.StaffId)) return new();
        return (await _db.Table<StaffRecurringScheduleDay>()
                .Where(item => item.ScheduleId == scheduleId).ToListAsync())
            .OrderBy(item => item.DayOfWeek).ToList();
    }

    public async Task<(StaffRecurringSchedule? Schedule, List<StaffRecurringScheduleDay> Days)> GetScheduleForDateAsync(
        int staffId, DateTime date)
    {
        await EnsureInitializedAsync();
        if (!CanManageStaff(staffId)) return (null, new());
        var schedules = await _db.Table<StaffRecurringSchedule>()
            .Where(item => item.StaffId == staffId).ToListAsync();
        var schedule = schedules
            .Where(item => item.IsActive && item.EffectiveStartDate.Date <= date.Date &&
                           (!item.EffectiveEndDate.HasValue || item.EffectiveEndDate.Value.Date >= date.Date))
            .OrderByDescending(item => item.EffectiveStartDate)
            .FirstOrDefault();
        if (schedule == null) return (null, new());
        var days = await _db.Table<StaffRecurringScheduleDay>()
            .Where(item => item.ScheduleId == schedule.Id).ToListAsync();
        return (schedule, days.OrderBy(item => item.DayOfWeek).ToList());
    }

    /// <summary>Returns active clinic employees and their schedule status for a date.</summary>
    public async Task<List<(UserAccount Staff, string ScheduleText, bool IsScheduled)>>
        GetStaffScheduleOverviewForDateAsync(DateTime date)
    {
        await EnsureInitializedAsync();
        if (!AuthService.IsClinicAdministrator(AuthService.Instance.CurrentUser)) return new();

        var staff = await AuthService.Instance.GetClinicStaffAsync(includeInactive: false);
        var recurring = await _db.Table<StaffRecurringSchedule>().ToListAsync();
        var dayRows = await _db.Table<StaffRecurringScheduleDay>().ToListAsync();
        var dateRows = await _db.Table<StaffShift>().ToListAsync();
        var leaves = await _db.Table<StaffLeave>().ToListAsync();
        var result = new List<(UserAccount Staff, string ScheduleText, bool IsScheduled)>();

        foreach (var employee in staff)
        {
            var leave = leaves.FirstOrDefault(item => item.StaffId == employee.Id && item.Status == "Approved" &&
                item.StartDate.Date <= date.Date && item.EndDate.Date >= date.Date);
            var intervals = GetIntervalsForDate(employee.Id, date.Date, recurring, dayRows, dateRows);

            var scheduleText = leave != null
                ? $"{leave.LeaveType} · through {leave.EndDate:MMM d}"
                : intervals.Count > 0
                    ? string.Join(" · ", intervals.Select(interval =>
                        $"{interval.Start:h:mm tt}–{interval.End:h:mm tt}"))
                    : dateRows.Any(item => item.StaffId == employee.Id && item.IsActive &&
                        item.ShiftDate.Date == date.Date && item.Status == "Rest Day")
                        ? "Rest day"
                        : dateRows.Any(item => item.StaffId == employee.Id && item.IsActive &&
                            item.ShiftDate.Date == date.Date && item.Status == "Unavailable")
                            ? "Unavailable today"
                            : "No shift scheduled";

            result.Add((employee, scheduleText, leave == null && intervals.Count > 0));
        }

        return result.OrderByDescending(item => item.IsScheduled)
            .ThenBy(item => item.Staff.Role)
            .ThenBy(item => item.Staff.FullName)
            .ToList();
    }

    public async Task<bool> SaveRecurringScheduleAsync(
        int staffId,
        DateTime effectiveStart,
        DateTime? effectiveEnd,
        IReadOnlyCollection<StaffRecurringScheduleDay> daySettings,
        string changeNote = "")
    {
        await EnsureInitializedAsync();
        var admin = AuthService.Instance.CurrentUser;
        if (!AuthService.IsClinicAdministrator(admin) || !await IsClinicStaffAsync(staffId)) return false;
        if (effectiveStart.Date < DateTime.Today || (effectiveEnd.HasValue && effectiveEnd.Value.Date < effectiveStart.Date))
            return false;
        if (daySettings.Count != 7 || daySettings.Select(day => day.DayOfWeek).Distinct().Count() != 7 ||
            daySettings.Any(day => day.DayOfWeek is < 0 or > 6 || !ShiftTypes.Contains(day.ShiftType, StringComparer.Ordinal) ||
                                   (day.DayOfWeek == (int)DayOfWeek.Sunday && day.IsWorkingDay)))
            return false;
        foreach (var day in daySettings.Where(day => day.IsWorkingDay))
        {
            if (!TryReadTime(day.StartTime, out var start) || !TryReadTime(day.EndTime, out var end) || start == end)
                return false;
        }
        if (HasOverlappingWeeklyIntervals(daySettings)) return false;

        await _db.RunInTransactionAsync(connection =>
        {
            var existing = connection.Table<StaffRecurringSchedule>()
                .ToList().Where(item => item.StaffId == staffId && item.IsActive).ToList();
            foreach (var previous in existing)
            {
                var oldEnd = previous.EffectiveEndDate?.Date ?? DateTime.MaxValue.Date;
                var newEnd = effectiveEnd?.Date ?? DateTime.MaxValue.Date;
                if (oldEnd < effectiveStart.Date || newEnd < previous.EffectiveStartDate.Date)
                    continue;

                if (previous.EffectiveStartDate.Date < effectiveStart.Date)
                {
                    var boundary = effectiveStart.Date.AddDays(-1);
                    if (!previous.EffectiveEndDate.HasValue || previous.EffectiveEndDate.Value.Date > boundary)
                        previous.EffectiveEndDate = boundary;
                }
                else
                {
                    // Keep superseded future versions as inactive history rather than deleting them.
                    previous.IsActive = false;
                }
                previous.ModifiedAt = DateTime.Now;
                previous.ModifiedByStaffId = admin!.Id;
                previous.ModifiedByName = admin.FullName;
                connection.Update(previous);
            }

            var schedule = new StaffRecurringSchedule
            {
                StaffId = staffId,
                EffectiveStartDate = effectiveStart.Date,
                EffectiveEndDate = effectiveEnd?.Date,
                IsActive = true,
                CreatedAt = DateTime.Now,
                CreatedByStaffId = admin!.Id,
                CreatedByName = admin.FullName,
                ChangeNote = changeNote.Trim()
            };
            connection.Insert(schedule);
            foreach (var setting in daySettings.OrderBy(day => day.DayOfWeek))
            {
                connection.Insert(new StaffRecurringScheduleDay
                {
                    ScheduleId = schedule.Id,
                    StaffId = staffId,
                    DayOfWeek = setting.DayOfWeek,
                    IsWorkingDay = setting.IsWorkingDay,
                    StartTime = setting.StartTime,
                    EndTime = setting.EndTime,
                    ShiftType = setting.ShiftType
                });
            }
        });

        await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(staffId);
        return true;
    }

    public async Task<bool> DeactivateRecurringScheduleAsync(int scheduleId)
    {
        await EnsureInitializedAsync();
        if (!AuthService.IsClinicAdministrator(AuthService.Instance.CurrentUser)) return false;
        var schedule = await _db.Table<StaffRecurringSchedule>().Where(item => item.Id == scheduleId).FirstOrDefaultAsync();
        if (schedule == null || !schedule.IsActive) return false;
        schedule.IsActive = false;
        schedule.ModifiedAt = DateTime.Now;
        schedule.ModifiedByStaffId = AuthService.Instance.CurrentUser!.Id;
        schedule.ModifiedByName = AuthService.Instance.CurrentUser.FullName;
        await _db.UpdateAsync(schedule);
        await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(schedule.StaffId);
        return true;
    }

    public async Task<bool> AddDateScheduleAsync(StaffShift shift)
    {
        await EnsureInitializedAsync();
        if (!AuthService.IsClinicAdministrator(AuthService.Instance.CurrentUser) ||
            shift.StaffId <= 0 || shift.ShiftDate.Date < DateTime.Today || !await IsClinicStaffAsync(shift.StaffId))
            return false;
        if (!DateScheduleTypes.Contains(shift.ScheduleType, StringComparer.Ordinal) ||
            !DateScheduleStatuses.Contains(shift.Status, StringComparer.Ordinal)) return false;
        if (shift.Status == "Rest Day" && shift.ScheduleType != "Override weekly schedule") return false;
        if (shift.Status != "Rest Day" && shift.EndsAt == shift.StartsAt) return false;

        var admin = AuthService.Instance.CurrentUser!;
        shift.ShiftDate = shift.ShiftDate.Date;
        shift.StartsAt = shift.ShiftDate.Add(shift.StartsAt.TimeOfDay);
        shift.EndsAt = shift.ShiftDate.Add(shift.EndsAt.TimeOfDay);
        if (shift.Status != "Rest Day" && shift.EndsAt <= shift.StartsAt)
            shift.EndsAt = shift.EndsAt.AddDays(1); // supports an overnight shift
        shift.IsActive = true;
        shift.CreatedAt = DateTime.Now;
        shift.CreatedByStaffId = admin.Id;
        shift.CreatedByName = admin.FullName;

        var existing = await _db.Table<StaffShift>().Where(item => item.StaffId == shift.StaffId).ToListAsync();
        foreach (var other in existing.Where(item => item.IsActive && item.ShiftDate.Date == shift.ShiftDate.Date &&
                                                      item.ScheduleType == shift.ScheduleType))
        {
            if (shift.Status == "Rest Day" || other.Status == "Rest Day") return false;
            if (other.Status == "Available" && shift.Status == "Available" &&
                other.StartsAt < shift.EndsAt && other.EndsAt > shift.StartsAt)
                return false;
        }

        var hasOtherScheduleKind = existing.Any(item => item.IsActive && item.ShiftDate.Date == shift.ShiftDate.Date &&
            ((shift.ScheduleType == "Additional shift" && item.ScheduleType is "Override" or "Override weekly schedule") ||
             (shift.ScheduleType == "Override weekly schedule" && item.ScheduleType == "Additional shift")));
        if (hasOtherScheduleKind) return false;

        if (shift.ScheduleType == "Additional shift" && shift.Status == "Available")
        {
            var recurring = await _db.Table<StaffRecurringSchedule>().Where(item => item.StaffId == shift.StaffId).ToListAsync();
            var dayRows = await _db.Table<StaffRecurringScheduleDay>().Where(item => item.StaffId == shift.StaffId).ToListAsync();
            var regularWindows = GetIntervalsForDate(shift.StaffId, shift.ShiftDate, recurring, dayRows, existing);
            if (regularWindows.Any(window => window.Start < shift.EndsAt && window.End > shift.StartsAt))
                return false;
        }

        await _db.InsertAsync(shift);
        await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(shift.StaffId);
        return true;
    }

    public async Task<bool> DeactivateDateScheduleAsync(int shiftId)
    {
        await EnsureInitializedAsync();
        if (!AuthService.IsClinicAdministrator(AuthService.Instance.CurrentUser)) return false;
        var shift = await _db.Table<StaffShift>().Where(item => item.Id == shiftId).FirstOrDefaultAsync();
        if (shift == null || !shift.IsActive) return false;
        shift.IsActive = false;
        shift.ModifiedAt = DateTime.Now;
        shift.ModifiedByStaffId = AuthService.Instance.CurrentUser!.Id;
        shift.ModifiedByName = AuthService.Instance.CurrentUser.FullName;
        await _db.UpdateAsync(shift);
        await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(shift.StaffId);
        return true;
    }

    public async Task<bool> AddLeaveAsync(StaffLeave leave)
    {
        await EnsureInitializedAsync();
        var admin = AuthService.Instance.CurrentUser;
        if (!AuthService.IsClinicAdministrator(admin) || !await IsClinicStaffAsync(leave.StaffId) ||
            leave.EndDate.Date < leave.StartDate.Date ||
            !LeaveTypes.Contains(leave.LeaveType, StringComparer.Ordinal)) return false;

        var existing = await _db.Table<StaffLeave>().Where(item => item.StaffId == leave.StaffId).ToListAsync();
        if (existing.Any(item => item.Status == "Approved" && item.StartDate.Date <= leave.EndDate.Date &&
                                 item.EndDate.Date >= leave.StartDate.Date)) return false;

        leave.StartDate = leave.StartDate.Date;
        leave.EndDate = leave.EndDate.Date;
        leave.Status = "Approved";
        leave.CreatedAt = DateTime.Now;
        leave.CreatedByStaffId = admin!.Id;
        leave.CreatedByName = admin.FullName;
        await _db.InsertAsync(leave);
        await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(leave.StaffId);
        return true;
    }

    public async Task<bool> CancelLeaveAsync(int leaveId)
    {
        await EnsureInitializedAsync();
        if (!AuthService.IsClinicAdministrator(AuthService.Instance.CurrentUser)) return false;
        var leave = await _db.Table<StaffLeave>().Where(item => item.Id == leaveId).FirstOrDefaultAsync();
        if (leave == null || leave.Status != "Approved") return false;
        leave.Status = "Cancelled";
        leave.ModifiedAt = DateTime.Now;
        leave.ModifiedByStaffId = AuthService.Instance.CurrentUser!.Id;
        leave.ModifiedByName = AuthService.Instance.CurrentUser.FullName;
        await _db.UpdateAsync(leave);
        await AppointmentService.Instance.ReassignAppointmentsFromStaffAsync(leave.StaffId);
        return true;
    }

    /// <summary>
    /// Returns an eligible active employee whose weekly/date schedule covers the entire slot,
    /// who has no approved leave or date block, and who has no overlapping appointment.
    /// It is called again inside the appointment transaction before saving/approving a booking.
    /// </summary>
    internal static int? FindAvailableStaffId(
        SQLiteConnection connection,
        string role,
        DateTime startsAt,
        DateTime endsAt,
        int slotIntervalMinutes,
        int ignoreAppointmentId = 0)
    {
        if (endsAt <= startsAt || slotIntervalMinutes <= 0) return null;

        var staff = connection.Table<UserAccount>().ToList()
            .Where(account => string.Equals(account.Role, role, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(account.AccountStatus, "Active", StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(account.EmploymentStatus, "Active", StringComparison.OrdinalIgnoreCase) &&
                              account.EmailVerified)
            .OrderBy(account => account.FullName).ToList();
        var appointments = connection.Table<Appointment>().ToList();
        var allStaffShifts = connection.Table<StaffShift>().ToList();
        var allRecurring = connection.Table<StaffRecurringSchedule>().ToList();
        var allDays = connection.Table<StaffRecurringScheduleDay>().ToList();
        var allLeave = connection.Table<StaffLeave>().ToList();
        var assignedCounts = appointments
            .Where(appointment => appointment.Id != ignoreAppointmentId &&
                                  appointment.VisitDate.Date == startsAt.Date &&
                                  appointment.AssignedStaffId.HasValue &&
                                  AppointmentOccupiesSlot(appointment.Status))
            .GroupBy(appointment => appointment.AssignedStaffId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var employee in staff.OrderBy(account => assignedCounts.GetValueOrDefault(account.Id))
                                       .ThenBy(account => account.FullName))
        {
            if (!HasScheduleCoverage(employee.Id, startsAt, endsAt, allRecurring, allDays, allStaffShifts, allLeave))
                continue;

            var hasConflict = appointments.Any(appointment =>
                appointment.Id != ignoreAppointmentId && appointment.AssignedStaffId == employee.Id &&
                AppointmentOccupiesSlot(appointment.Status) &&
                AppointmentService.GetVisitDateTime(appointment) < endsAt &&
                AppointmentService.GetVisitDateTime(appointment).AddMinutes(slotIntervalMinutes) > startsAt);
            if (!hasConflict) return employee.Id;
        }
        return null;
    }

    public async Task<List<string>> GetWeeklyPreviewAsync(int staffId, DateTime weekStart)
    {
        await EnsureInitializedAsync();
        if (!CanManageStaff(staffId)) return new();
        var recurring = await _db.Table<StaffRecurringSchedule>().Where(item => item.StaffId == staffId).ToListAsync();
        var days = await _db.Table<StaffRecurringScheduleDay>().Where(item => item.StaffId == staffId).ToListAsync();
        var dateSchedules = await _db.Table<StaffShift>().Where(item => item.StaffId == staffId).ToListAsync();
        var leaves = await _db.Table<StaffLeave>().Where(item => item.StaffId == staffId).ToListAsync();
        return Enumerable.Range(0, 7).Select(offset =>
        {
            var date = weekStart.Date.AddDays(offset);
            if (leaves.Any(item => item.Status == "Approved" && item.StartDate.Date <= date && item.EndDate.Date >= date))
                return $"{date:ddd, MMM d} · Leave / unavailable";
            var intervals = GetIntervalsForDate(staffId, date, recurring, days, dateSchedules);
            return intervals.Count > 0
                ? $"{date:ddd, MMM d} · {intervals[0].Start:h:mm tt}–{intervals[0].End:h:mm tt}"
                : $"{date:ddd, MMM d} · Rest day / no schedule";
        }).ToList();
    }

    private static bool HasScheduleCoverage(
        int staffId, DateTime startsAt, DateTime endsAt,
        List<StaffRecurringSchedule> recurring,
        List<StaffRecurringScheduleDay> days,
        List<StaffShift> dateSchedules,
        List<StaffLeave> leaves)
    {
        if (leaves.Any(item => item.StaffId == staffId && item.Status == "Approved" &&
                               item.StartDate.Date <= endsAt.Date && item.EndDate.Date >= startsAt.Date))
            return false;

        // The previous day's night shift may carry into this appointment date.
        var dateCandidates = new[] { startsAt.Date.AddDays(-1), startsAt.Date, endsAt.Date };
        var windows = dateCandidates
            .SelectMany(date => GetIntervalsForDate(staffId, date, recurring, days, dateSchedules))
            .ToList();
        if (!windows.Any(window => window.Start <= startsAt && window.End >= endsAt)) return false;

        foreach (var blocked in dateSchedules.Where(item => item.StaffId == staffId && item.IsActive &&
                     item.Status != "Available" && item.ShiftDate.Date >= startsAt.Date.AddDays(-1) &&
                     item.ShiftDate.Date <= endsAt.Date))
        {
            var blockStart = blocked.Status == "Rest Day" ? blocked.ShiftDate.Date : blocked.StartsAt;
            var blockEnd = blocked.Status == "Rest Day" ? blocked.ShiftDate.Date.AddDays(1) : blocked.EndsAt;
            if (blockStart < endsAt && blockEnd > startsAt) return false;
        }
        return true;
    }

    private static List<(DateTime Start, DateTime End)> GetIntervalsForDate(
        int staffId, DateTime date,
        List<StaffRecurringSchedule> recurring,
        List<StaffRecurringScheduleDay> days,
        List<StaffShift> dateSchedules)
    {
        if (date.DayOfWeek == DayOfWeek.Sunday) return new();

        var entries = dateSchedules.Where(item => item.StaffId == staffId && item.IsActive &&
                                                  item.ShiftDate.Date == date.Date).ToList();
        // "Override" is the value used by dated shifts saved by earlier app versions.
        var overrides = entries.Where(item => item.ScheduleType is "Override weekly schedule" or "Override").ToList();
        if (overrides.Count > 0)
            return overrides.Where(item => item.Status == "Available")
                .Select(item => (item.StartsAt, item.EndsAt)).ToList();

        var intervals = new List<(DateTime Start, DateTime End)>();
        var schedule = recurring.Where(item => item.StaffId == staffId && item.IsActive &&
                                               item.EffectiveStartDate.Date <= date.Date &&
                                               (!item.EffectiveEndDate.HasValue || item.EffectiveEndDate.Value.Date >= date.Date))
            .OrderByDescending(item => item.EffectiveStartDate).FirstOrDefault();
        if (schedule != null)
        {
            var day = days.FirstOrDefault(item => item.ScheduleId == schedule.Id && item.DayOfWeek == (int)date.DayOfWeek);
            if (day is { IsWorkingDay: true } && TryReadTime(day.StartTime, out var start) &&
                TryReadTime(day.EndTime, out var end) && start != end)
                intervals.Add(BuildInterval(date.Date, start, end));
        }
        intervals.AddRange(entries.Where(item => item.ScheduleType == "Additional shift" && item.Status == "Available")
            .Select(item => (item.StartsAt, item.EndsAt)));
        return intervals;
    }

    private async Task<bool> IsClinicStaffAsync(int staffId)
    {
        var account = await AuthService.Instance.GetAccountByDatabaseIdAsync(staffId);
        return account != null && AuthService.IsClinicStaffRole(account.Role);
    }

    private bool CanManageStaff(int staffId)
    {
        var current = AuthService.Instance.CurrentUser;
        return current != null && (AuthService.IsClinicAdministrator(current) || current.Id == staffId);
    }

    private static bool AppointmentOccupiesSlot(string status) =>
        status is "Pending" or "Confirmed" or "CheckedIn" or "VitalsRecorded";

    private static bool HasOverlappingWeeklyIntervals(IReadOnlyCollection<StaffRecurringScheduleDay> daySettings)
    {
        var anchorSunday = new DateTime(2024, 1, 7); // Sunday, used only to compare weekday intervals.
        var intervals = new List<(DateTime Start, DateTime End)>();
        foreach (var weekOffset in new[] { -1, 0, 1 })
        foreach (var day in daySettings.Where(item => item.IsWorkingDay))
        {
            if (!TryReadTime(day.StartTime, out var start) || !TryReadTime(day.EndTime, out var end) || start == end)
                continue;
            var shift = BuildInterval(anchorSunday.AddDays(weekOffset * 7 + day.DayOfWeek), start, end);
            intervals.Add(shift);
        }
        for (var i = 0; i < intervals.Count; i++)
        for (var j = i + 1; j < intervals.Count; j++)
            if (intervals[i].Start < intervals[j].End && intervals[i].End > intervals[j].Start)
                return true;
        return false;
    }

    private static bool TryReadTime(string text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out time);

    private static (DateTime Start, DateTime End) BuildInterval(DateTime date, TimeSpan start, TimeSpan end)
    {
        var startsAt = date.Date.Add(start);
        var endsAt = date.Date.Add(end);
        if (endsAt <= startsAt) endsAt = endsAt.AddDays(1);
        return (startsAt, endsAt);
    }
}
