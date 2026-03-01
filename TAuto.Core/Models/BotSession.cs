using System;

namespace TAuto.Core.Models;

/// <summary>
/// Tracks per-bot session data that persists across runs.
/// Loaded on startup, saved periodically and on graceful shutdown.
/// </summary>
public class BotSession
{
    public string BotId { get; set; } = string.Empty;

    /// <summary>Cumulative play time across all sessions.</summary>
    public double TotalPlayTimeHours { get; set; }

    /// <summary>Last time the bot logged in.</summary>
    public DateTime? LastLoginUtc { get; set; }

    /// <summary>Last time the bot logged out.</summary>
    public DateTime? LastLogoutUtc { get; set; }

    /// <summary>Average session length in minutes (rolling average).</summary>
    public double AverageSessionMinutes { get; set; }

    /// <summary>Number of sessions completed.</summary>
    public int SessionCount { get; set; }

    /// <summary>Current session start time (transient, not saved).</summary>
    public DateTime? CurrentSessionStart { get; set; }

    /// <summary>Cumulative micro-break downtime in the current session (minutes).</summary>
    public double CurrentSessionDowntimeMinutes { get; set; }

    /// <summary>
    /// Real-time risk score (0–100). Calculated by RiskMonitorService.
    /// Higher = more bot-like behavior detected. Transient.
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>Mark session start.</summary>
    public void StartSession()
    {
        CurrentSessionStart = DateTime.UtcNow;
        CurrentSessionDowntimeMinutes = 0;
        RiskScore = 0;
        LastLoginUtc = DateTime.UtcNow;
    }

    /// <summary>Mark session end and update rolling stats.</summary>
    public void EndSession()
    {
        if (CurrentSessionStart.HasValue)
        {
            var duration = (DateTime.UtcNow - CurrentSessionStart.Value).TotalMinutes;
            TotalPlayTimeHours += duration / 60.0;
            SessionCount++;
            // Rolling average
            AverageSessionMinutes = AverageSessionMinutes == 0
                ? duration
                : (AverageSessionMinutes * (SessionCount - 1) + duration) / SessionCount;
            LastLogoutUtc = DateTime.UtcNow;
            CurrentSessionStart = null;
        }
    }
}
