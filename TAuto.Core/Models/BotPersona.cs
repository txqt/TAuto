using System;
using System.Collections.Generic;

namespace TAuto.Core.Models;

/// <summary>
/// Defines a unique behavioral "personality" for each bot instance.
/// Each bot gets a different persona so that 100 bots produce 100 distinct
/// behavioral fingerprints, defeating statistical clustering.
/// Persisted to JSON and drifted ±5% daily.
/// </summary>
public class BotPersona
{
    /// <summary>Unique identifier, typically the worker/account ID.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Multiplier for all reaction/action delays.
    /// 0.8 = fast player, 1.0 = average, 1.4 = slow/cautious.
    /// </summary>
    public double SpeedMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Multiplier for coordinate random offset range.
    /// 0.7 = precise clicker, 1.0 = average, 1.5 = sloppy.
    /// </summary>
    public double AccuracyMultiplier { get; set; } = 1.0;

    /// <summary>
    /// How long the bot persists before giving up on a failing action (0.0–1.0).
    /// Higher = more patient (more retries, longer waits).
    /// </summary>
    public double PatienceLevel { get; set; } = 0.5;

    /// <summary>
    /// Maximum allowed total downtime (minutes) from injected micro-breaks per session.
    /// Prevents over-randomization from tanking farming ROI.
    /// </summary>
    public int SlaMaxDowntimeMinutes { get; set; } = 15;

    /// <summary>
    /// Preferred active hours (0–23). Empty = always active.
    /// Used by SchedulerService for session start/stop times.
    /// </summary>
    public List<int> ActiveHours { get; set; } = new();

    /// <summary>UTC timestamp of the last daily drift update.</summary>
    public DateTime LastDriftUtc { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Generate a random persona with Gaussian-distributed parameters.
    /// Each call produces a statistically unique profile.
    /// </summary>
    public static BotPersona GenerateRandom(string id)
    {
        var rng = new Random(id.GetHashCode() ^ Environment.TickCount);
        return new BotPersona
        {
            Id = id,
            SpeedMultiplier = Clamp(GaussianSample(rng, 1.0, 0.15), 0.6, 1.6),
            AccuracyMultiplier = Clamp(GaussianSample(rng, 1.0, 0.12), 0.6, 1.5),
            PatienceLevel = Clamp(GaussianSample(rng, 0.5, 0.15), 0.1, 0.95),
            SlaMaxDowntimeMinutes = rng.Next(8, 25),
            LastDriftUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Apply daily drift: shift each parameter by ±5% Gaussian noise.
    /// Idempotent per day (checks LastDriftUtc).
    /// </summary>
    public void ApplyDailyDrift()
    {
        if ((DateTime.UtcNow - LastDriftUtc).TotalHours < 20) return; // Already drifted today

        var rng = new Random(Id.GetHashCode() ^ DateTime.UtcNow.DayOfYear);
        SpeedMultiplier = Clamp(SpeedMultiplier * GaussianSample(rng, 1.0, 0.03), 0.6, 1.6);
        AccuracyMultiplier = Clamp(AccuracyMultiplier * GaussianSample(rng, 1.0, 0.03), 0.6, 1.5);
        PatienceLevel = Clamp(PatienceLevel + GaussianSample(rng, 0.0, 0.02), 0.1, 0.95);
        LastDriftUtc = DateTime.UtcNow;
    }

    // ── Helpers ──

    /// <summary>Box-Muller Gaussian sample.</summary>
    internal static double GaussianSample(Random rng, double mean, double stdDev)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = 1.0 - rng.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return mean + stdDev * z;
    }

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));
}
