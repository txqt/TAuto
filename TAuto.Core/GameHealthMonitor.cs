using System;
using System.Diagnostics;
using TAuto.Core.Imaging;

namespace TAuto.Core;

/// <summary>
/// Health status of the game being automated.
/// </summary>
public enum GameHealthStatus
{
    Healthy,
    CaptureFailure,
    FrozenFrame,
    NoActivity,
    GameCrashed
}

/// <summary>
/// Monitors game health by detecting capture failures, frozen frames,
/// and inactivity. Fires OnUnhealthy when intervention is needed.
/// </summary>
public class GameHealthMonitor
{
    private int _consecutiveCaptureFailures;
    private int _consecutiveSameFrames;
    private uint _lastFrameHash;
    private DateTime _lastActivityTime = DateTime.UtcNow;
    private GameHealthStatus _status = GameHealthStatus.Healthy;

    /// <summary>
    /// Max consecutive capture failures before declaring game crashed.
    /// Default: 5.
    /// </summary>
    public int MaxCaptureFailures { get; set; } = 5;

    /// <summary>
    /// Max consecutive identical frames before declaring game frozen.
    /// Default: 10.
    /// </summary>
    public int MaxFrozenFrames { get; set; } = 10;

    /// <summary>
    /// Max time with no state transitions before declaring bot stuck.
    /// Default: 5 minutes.
    /// </summary>
    public TimeSpan MaxNoActivityTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Current health status.
    /// </summary>
    public GameHealthStatus Status => _status;

    /// <summary>
    /// Fired when the game is detected as unhealthy.
    /// </summary>
    public event Action<GameHealthStatus>? OnUnhealthy;

    /// <summary>
    /// Call this after each screen capture attempt.
    /// Returns true if game is healthy, false if intervention is needed.
    /// </summary>
    public bool ReportCaptureResult(IImage? capture)
    {
        if (capture == null)
        {
            _consecutiveCaptureFailures++;
            if (_consecutiveCaptureFailures >= MaxCaptureFailures)
            {
                SetUnhealthy(GameHealthStatus.CaptureFailure);
                return false;
            }
            return true;
        }

        // Reset failure counter on successful capture
        _consecutiveCaptureFailures = 0;

        // Check for frozen frame via simple hash
        uint hash = ComputeSimpleHash(capture);
        if (hash == _lastFrameHash)
        {
            _consecutiveSameFrames++;
            if (_consecutiveSameFrames >= MaxFrozenFrames)
            {
                SetUnhealthy(GameHealthStatus.FrozenFrame);
                return false;
            }
        }
        else
        {
            _consecutiveSameFrames = 0;
            _lastFrameHash = hash;
        }

        return true;
    }

    /// <summary>
    /// Call this whenever the bot performs a meaningful action or state transition.
    /// Resets the no-activity timer.
    /// </summary>
    public void ReportActivity()
    {
        _lastActivityTime = DateTime.UtcNow;
        _status = GameHealthStatus.Healthy;
    }

    /// <summary>
    /// Check if the bot has been inactive for too long.
    /// Call this periodically (e.g., in the FSM polling loop).
    /// </summary>
    public bool CheckActivityTimeout()
    {
        if (DateTime.UtcNow - _lastActivityTime > MaxNoActivityTimeout)
        {
            SetUnhealthy(GameHealthStatus.NoActivity);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Reset all counters. Call after a successful game restart.
    /// </summary>
    public void Reset()
    {
        _consecutiveCaptureFailures = 0;
        _consecutiveSameFrames = 0;
        _lastFrameHash = 0;
        _lastActivityTime = DateTime.UtcNow;
        _status = GameHealthStatus.Healthy;
    }

    private void SetUnhealthy(GameHealthStatus status)
    {
        _status = status;
        Debug.WriteLine($"[GameHealthMonitor] ⚠️ Unhealthy: {status}");
        OnUnhealthy?.Invoke(status);
    }

    /// <summary>
    /// Fast, low-cost hash of an IImage for frozen frame detection.
    /// Samples a grid of pixels rather than hashing the entire image.
    /// </summary>
    private static uint ComputeSimpleHash(IImage source)
    {
        try
        {
            int w = source.Width;
            int h = source.Height;
            int stride = (w * 4); // Assume 32bpp BGRA

            // Sample 16 evenly-spaced pixels
            uint hash = 2166136261u; // FNV-1a offset basis
            int stepX = Math.Max(1, w / 4);
            int stepY = Math.Max(1, h / 4);

            byte[] pixels = source.GetPixelData();
            
            for (int y = stepY; y < h; y += stepY)
            {
                int rowOffset = y * stride;
                for (int x = stepX; x < w; x += stepX)
                {
                    int offset = rowOffset + (x * 4);
                    if (offset + 2 < pixels.Length)
                    {
                        hash ^= pixels[offset];
                        hash *= 16777619u;
                        hash ^= pixels[offset + 1];
                        hash *= 16777619u;
                        hash ^= pixels[offset + 2];
                        hash *= 16777619u;
                    }
                }
            }

            return hash;
        }
        catch
        {
            return 0;
        }
    }
}
