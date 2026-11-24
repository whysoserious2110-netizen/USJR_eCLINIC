using System.Globalization;

namespace USJR_eCLINIC.Services;

public sealed record CheckInPolicyDecision(
    bool CanCheckIn,
    bool IsLate,
    bool RequiresReschedule,
    string Message);

public static class AppointmentShiftPolicy
{
    public const int AppointmentDurationMinutes = 60;
    public const string MorningShift = "Morning";
    public const string AfternoonShift = "Afternoon";

    public static readonly TimeSpan MorningStart = new(7, 0, 0);
    public static readonly TimeSpan MorningEnd = new(12, 0, 0);
    public static readonly TimeSpan MorningLastCheckIn = new(11, 0, 0);
    public static readonly TimeSpan BreakStart = new(12, 0, 0);
    public static readonly TimeSpan BreakEnd = new(13, 0, 0);
    public static readonly TimeSpan AfternoonStart = new(13, 0, 0);
    public static readonly TimeSpan AfternoonEnd = new(19, 0, 0);
    public static readonly TimeSpan AfternoonLastCheckIn = new(18, 0, 0);

    public static string? GetShiftForAppointmentStart(TimeSpan start)
    {
        var end = start.Add(TimeSpan.FromMinutes(AppointmentDurationMinutes));

        if (start >= MorningStart && start < MorningEnd && end <= MorningEnd)
            return MorningShift;

        if (start >= AfternoonStart && start < AfternoonEnd && end <= AfternoonEnd)
            return AfternoonShift;

        return null;
    }

    public static IReadOnlyList<TimeSpan> GetBookableStartTimes() =>
        Enumerable.Range(0, (int)(MorningLastCheckIn - MorningStart).TotalMinutes + 1)
            .Select(offset => MorningStart.Add(TimeSpan.FromMinutes(offset)))
            .Concat(Enumerable.Range(0, (int)(AfternoonLastCheckIn - AfternoonStart).TotalMinutes + 1)
                .Select(offset => AfternoonStart.Add(TimeSpan.FromMinutes(offset))))
            .ToArray();

    public static bool IsBookableAppointmentStart(TimeSpan start)
    {
        if (start.Ticks % TimeSpan.TicksPerMinute != 0)
            return false;

        return (start >= MorningStart && start <= MorningLastCheckIn) ||
               (start >= AfternoonStart && start <= AfternoonLastCheckIn);
    }
    public static bool TryParseTime(string? timeText, out TimeSpan time)
    {
        if (DateTime.TryParse(
                timeText,
                CultureInfo.CurrentCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            time = parsed.TimeOfDay;
            return true;
        }

        time = default;
        return false;
    }

    public static CheckInPolicyDecision EvaluateCheckIn(
        string? assignedShift,
        DateTime appointmentStart,
        DateTime arrivalTime)
    {
        if (appointmentStart.Date != arrivalTime.Date)
        {
            return new(false, false, false, "Check-in is available only on the appointment date.");
        }

        var actualShift = GetShiftForAppointmentStart(appointmentStart.TimeOfDay);
        if (actualShift == null || string.IsNullOrWhiteSpace(assignedShift) ||
            !string.Equals(assignedShift, actualShift, StringComparison.OrdinalIgnoreCase))
        {
            return new(
                false,
                false,
                true,
                "This appointment does not have a valid shift assignment. Please request a new appointment schedule.");
        }

        var shiftStart = actualShift == MorningShift ? MorningStart : AfternoonStart;
        var shiftEnd = actualShift == MorningShift ? MorningEnd : AfternoonEnd;
        var lastCheckIn = actualShift == MorningShift ? MorningLastCheckIn : AfternoonLastCheckIn;

        if (arrivalTime.TimeOfDay < shiftStart)
        {
            var opening = actualShift == MorningShift ? "7:00 AM" : "1:00 PM";
            return new(false, false, false,
                $"Your appointment is assigned to the {actualShift} Shift. Check-in opens at {opening}.");
        }

        if (arrivalTime.TimeOfDay >= shiftEnd)
            return new(false, false, true, ShiftEndedMessage(actualShift));

        var arrivalMinute = new TimeSpan(arrivalTime.Hour, arrivalTime.Minute, 0);
        if (arrivalMinute > lastCheckIn)
        {
            var cutoff = DateTime.Today.Add(lastCheckIn).ToString("h:mm tt", CultureInfo.CurrentCulture);
            return new(false, true, true,
                $"The {actualShift} Shift check-in window ended at {cutoff}. Please request a new appointment schedule.");
        }

        var isLate = arrivalTime > appointmentStart;
        var lastCheckInText = DateTime.Today.Add(lastCheckIn).ToString("h:mm tt", CultureInfo.CurrentCulture);
        return new(
            true,
            isLate,
            false,
            isLate
                ? $"Late arrival. Check-in is allowed until {lastCheckInText} during your assigned shift."
                : $"Check-in is allowed during your {actualShift} Shift.");
    }
    public static string ShiftEndedMessage(string shift) => shift == MorningShift
        ? "Your appointment was scheduled for the Morning Shift, which has already ended. You cannot use your morning appointment for afternoon check-in. Please request a new appointment schedule."
        : "Your appointment was scheduled for the Afternoon Shift, which has already ended. You cannot use your afternoon appointment for morning check-in. Please request a new appointment schedule.";

    public static string GetSlotPolicyError(TimeSpan start)
    {
        if (start >= BreakStart && start < BreakEnd)
            return "Appointments are not available during the 12:00 PM–1:00 PM break. Choose a Morning or Afternoon Shift time.";

        if (start >= MorningStart && start < MorningEnd || start >= AfternoonStart && start < AfternoonEnd)
            return "Choose a one-hour appointment start between 7:00 AM and 11:00 AM, or 1:00 PM and 6:00 PM.";

        return "Choose an appointment start within clinic hours: Morning Shift 7:00–11:00 AM or Afternoon Shift 1:00–6:00 PM. Each appointment lasts one hour.";
    }
}
