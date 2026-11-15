using SQLite;

namespace USJR_eCLINIC.Models;

/// <summary>A one-time clinic schedule or date exception. StaffId references UserAccount.Id.</summary>
public sealed class StaffShift
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int StaffId { get; set; }

    public DateTime ShiftDate { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    // Override replaces the regular weekly schedule for this date. Additional adds
    // an extra interval to the regular schedule. Existing rows migrate as Override.
    public string ScheduleType { get; set; } = "Override";
    public string Status { get; set; } = "Available"; // Available, Rest Day, Unavailable
    public string Notes { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CreatedByStaffId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime? ModifiedAt { get; set; }
    public int? ModifiedByStaffId { get; set; }
    public string ModifiedByName { get; set; } = string.Empty;

    [Ignore]
    public string ScheduleDisplay => Status == "Rest Day"
        ? $"{ShiftDate:ddd, MMM d, yyyy} · Rest day ({ScheduleType})"
        : $"{ShiftDate:ddd, MMM d, yyyy} · {StartsAt:h:mm tt}–{EndsAt:h:mm tt} ({ScheduleType})";

    [Ignore]
    public bool IsInactive => !IsActive;
}
