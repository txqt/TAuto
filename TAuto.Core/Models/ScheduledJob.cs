using System;

namespace TAuto.Core.Models;

public enum ConflictPolicy
{
    Skip,
    Queue,
    Cancel
}

public enum ScheduleType
{
    Cron,
    Interval
}

public class ScheduledJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Job";
    public string ScriptPath { get; set; } = string.Empty;
    public string DeviceSerial { get; set; } = string.Empty;
    
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Interval;
    public string CronExpression { get; set; } = "* * * * *"; // Default every minute
    public int IntervalMinutes { get; set; } = 30;
    
    public ConflictPolicy ConflictPolicy { get; set; } = ConflictPolicy.Skip;
    public int QueueMaxSize { get; set; } = 1;
    
    public bool AutoDisableOnError { get; set; }
    public int MaxConsecutiveFailures { get; set; } = 3;
    public int ConsecutiveFailures { get; set; }
    
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public string LastStatus { get; set; } = "Idle";

    /// <summary>
    /// Gaussian variance (minutes) applied to NextRunTime for fleet de-correlation.
    /// Prevents 100 bots from starting at the exact same time.
    /// Default 15 = spread of ±15 minutes.
    /// </summary>
    public int StartupVarianceMinutes { get; set; } = 15;
}
