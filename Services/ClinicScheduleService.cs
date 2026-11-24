using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace USJR_eCLINIC.Services;

public sealed class ClinicScheduleService
{
    public static ClinicScheduleService Instance { get; } = new();

    private const string AssetName = "clinic_schedule.json";
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private ClinicScheduleConfiguration? _configuration;

    private ClinicScheduleService()
    {
    }

    public async Task<bool> IsDateAvailableAsync(
        DateTime date,
        string serviceType,
        string subService)
    {
        return await GetDateUnavailableReasonAsync(date, serviceType, subService) == null;
    }

    public async Task<int> GetSlotIntervalMinutesAsync()
    {
        var interval = (await GetConfigurationAsync()).SlotIntervalMinutes;
        return interval > 0 ? interval : AppointmentShiftPolicy.AppointmentDurationMinutes;
    }

    public async Task<string?> GetDateUnavailableReasonAsync(
        DateTime date,
        string serviceType,
        string subService)
    {
        if (date.Date < DateTime.Today)
            return "Please choose a future date.";

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return "The clinic does not accept appointments on Sundays. Please choose another day.";

        var configuration = await GetConfigurationAsync();
        var dateKey = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (configuration.Holidays.Contains(dateKey, StringComparer.Ordinal))
            return "This date is a school holiday.";

        if (FindRule(configuration, serviceType, subService, date.DayOfWeek) == null)
            return "This service is not available on the selected date. Please choose another date.";

        return null;
    }

    public async Task<List<string>> GetAvailableTimeOptionsAsync(
        DateTime date,
        string serviceType,
        string subService)
    {
        if (date.Date < DateTime.Today)
            return new List<string>();

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return new List<string>();

        var configuration = await GetConfigurationAsync();
        var dateKey = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (configuration.Holidays.Contains(dateKey, StringComparer.Ordinal))
            return new List<string>();

        var rule = FindRule(configuration, serviceType, subService, date.DayOfWeek);
        if (rule == null ||
            !TimeSpan.TryParseExact(rule.Start, @"hh\:mm", CultureInfo.InvariantCulture, out var start) ||
            !TimeSpan.TryParseExact(rule.End, @"hh\:mm", CultureInfo.InvariantCulture, out var end) ||
            end <= start)
        {
            return new List<string>();
        }

        var options = new List<string>();

        foreach (var time in AppointmentShiftPolicy.GetBookableStartTimes())
        {
            var shift = AppointmentShiftPolicy.GetShiftForAppointmentStart(time);
            var appointmentEnd = time.Add(TimeSpan.FromMinutes(AppointmentShiftPolicy.AppointmentDurationMinutes));
            if (shift == null || time < start || appointmentEnd > end)
                continue;

            if (date.Date == DateTime.Today && time <= DateTime.Now.TimeOfDay)
                continue;

            options.Add(DateTime.Today.Add(time).ToString("h:mm tt", CultureInfo.CurrentCulture));
        }

        return options;
    }

    public async Task<DateTime?> GetClinicClosingDateTimeAsync(
        DateTime date,
        string serviceType,
        string subService)
    {
        var configuration = await GetConfigurationAsync();
        var dateKey = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (configuration.Holidays.Contains(dateKey, StringComparer.Ordinal))
            return date.Date;

        var matchingRules = configuration.Rules
            .Where(rule =>
                string.Equals(rule.ServiceType, serviceType, StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(rule.SubService, subService, StringComparison.OrdinalIgnoreCase) ||
                 rule.SubService == "*") &&
                rule.Days.Contains(date.DayOfWeek))
            .ToList();
        var exactRules = matchingRules
            .Where(rule => string.Equals(rule.SubService, subService, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var applicableRules = exactRules.Count > 0
            ? exactRules
            : matchingRules.Where(rule => rule.SubService == "*").ToList();
        var closingTimes = applicableRules
            .Select(rule => TimeSpan.TryParseExact(
                rule.End,
                @"hh\:mm",
                CultureInfo.InvariantCulture,
                out var end) ? (TimeSpan?)end : null)
            .Where(time => time.HasValue)
            .Select(time => time!.Value)
            .ToList();
        return closingTimes.Count == 0
            ? null
            : date.Date.Add(closingTimes.Max());
    }
    public async Task<string?> ValidateSlotAsync(
        DateTime date,
        string serviceType,
        string subService,
        string timeText)
    {
        if (date.Date < DateTime.Today)
            return "Please choose a future date.";

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return "The clinic does not accept appointments on Sundays. Please choose another day.";

        if (!DateTime.TryParse(timeText, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsedTime))
            return "Please select a valid appointment time.";

        var time = parsedTime.TimeOfDay;
        if (date.Date == DateTime.Today && date.Date.Add(time) <= DateTime.Now)
            return "That appointment time has already passed. Please choose a future time.";

        if (!AppointmentShiftPolicy.IsBookableAppointmentStart(time) || AppointmentShiftPolicy.GetShiftForAppointmentStart(time) == null)
            return AppointmentShiftPolicy.GetSlotPolicyError(time);

        var configuration = await GetConfigurationAsync();
        var dateKey = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        if (configuration.Holidays.Contains(dateKey, StringComparer.Ordinal))
            return "This date is a school holiday.";

        var rule = FindRule(configuration, serviceType, subService, date.DayOfWeek);
        if (rule == null)
            return "This service is not available on the selected date.";

        if (!TimeSpan.TryParseExact(rule.Start, @"hh\:mm", CultureInfo.InvariantCulture, out var start) ||
            !TimeSpan.TryParseExact(rule.End, @"hh\:mm", CultureInfo.InvariantCulture, out var end) ||
            end <= start)
        {
            return "The clinic schedule is not configured for this service.";
        }

        var appointmentEnd = time.Add(TimeSpan.FromMinutes(AppointmentShiftPolicy.AppointmentDurationMinutes));
        if (!AppointmentShiftPolicy.IsBookableAppointmentStart(time) ||
            time < start ||
            time >= end ||
            appointmentEnd > end)
        {
            return "That time is outside the clinic's available appointment slots.";
        }

        return null;
    }

    private static ClinicScheduleRule? FindRule(
        ClinicScheduleConfiguration configuration,
        string serviceType,
        string subService,
        DayOfWeek day)
    {
        return configuration.Rules.FirstOrDefault(rule =>
            string.Equals(rule.ServiceType, serviceType, StringComparison.OrdinalIgnoreCase) &&
            (rule.SubService == "*" ||
             string.Equals(rule.SubService, subService, StringComparison.OrdinalIgnoreCase)) &&
            rule.Days.Contains(day));
    }

    private async Task<ClinicScheduleConfiguration> GetConfigurationAsync()
    {
        if (_configuration != null)
            return _configuration;

        await _loadLock.WaitAsync();
        try
        {
            if (_configuration != null)
                return _configuration;

            await using var stream = await FileSystem.OpenAppPackageFileAsync(AssetName);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new JsonStringEnumConverter());

            _configuration = await JsonSerializer.DeserializeAsync<ClinicScheduleConfiguration>(
                    stream,
                    options)
                ?? throw new InvalidOperationException("The clinic schedule configuration is empty.");

            return _configuration;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public sealed class ClinicScheduleConfiguration
    {
        public int SlotIntervalMinutes { get; set; } = AppointmentShiftPolicy.AppointmentDurationMinutes;
        public List<string> Holidays { get; set; } = new();
        public List<ClinicScheduleRule> Rules { get; set; } = new();
    }

    public sealed class ClinicScheduleRule
    {
        public string ServiceType { get; set; } = string.Empty;
        public string SubService { get; set; } = "*";
        public List<DayOfWeek> Days { get; set; } = new();
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
    }
}


