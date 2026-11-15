using SQLite;

namespace USJR_eCLINIC.Models;

/// <summary>A versioned weekly schedule; old versions remain for schedule history.</summary>
public sealed class StaffRecurringSchedule
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int StaffId { get; set; }

    public DateTime EffectiveStartDate { get; set; }
    public DateTime? EffectiveEndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedByStaffId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string ChangeNote { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedByStaffId { get; set; }
    public string ModifiedByName { get; set; } = string.Empty;
}

/// <summary>One weekday in a versioned weekly schedule. Times use invariant HH:mm text.</summary>
public sealed class StaffRecurringScheduleDay
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ScheduleId { get; set; }

    [Indexed]
    public int StaffId { get; set; }

    public int DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; }
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "17:00";
    public string ShiftType { get; set; } = "Custom";
}

/// <summary>An administrator-recorded absence or unavailable date range.</summary>
public sealed class StaffLeave
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int StaffId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string LeaveType { get; set; } = "Unavailable";
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Approved";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedByStaffId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedByStaffId { get; set; }
    public string ModifiedByName { get; set; } = string.Empty;

    [Ignore]
    public bool IsApproved => Status == "Approved";

    [Ignore]
    public bool IsCancelled => Status != "Approved";
}
