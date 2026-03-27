using Cronos;

namespace TAuto.Core.Models;

public class ScheduleDefinition
{
    public ScheduleType Type { get; set; } = ScheduleType.Interval;
    public string? CronExpression { get; set; }
    public int IntervalMinutes { get; set; }
    public int StartupVarianceMinutes { get; set; }
}

public static class ScheduleCalculator
{
    public static DateTime? ComputeNextRunUtc(
        ScheduleDefinition? schedule,
        bool isEnabled,
        DateTime utcNow,
        Func<int, int>? varianceProvider = null)
    {
        if (!isEnabled || schedule == null)
            return null;

        int variance = schedule.StartupVarianceMinutes > 0
            ? Math.Max(0, varianceProvider?.Invoke(schedule.StartupVarianceMinutes) ?? Random.Shared.Next(0, schedule.StartupVarianceMinutes))
            : 0;

        if (schedule.Type == ScheduleType.Interval && schedule.IntervalMinutes > 0)
            return utcNow.AddMinutes(schedule.IntervalMinutes + variance);

        if (schedule.Type == ScheduleType.Cron && !string.IsNullOrWhiteSpace(schedule.CronExpression))
        {
            try
            {
                var cron = CronExpression.Parse(schedule.CronExpression);
                var nextOccurrence = cron.GetNextOccurrence(utcNow, TimeZoneInfo.Utc);
                if (nextOccurrence.HasValue && variance > 0)
                    nextOccurrence = nextOccurrence.Value.AddMinutes(variance);
                return nextOccurrence;
            }
            catch (CronFormatException)
            {
                return null;
            }
        }

        return null;
    }
}
