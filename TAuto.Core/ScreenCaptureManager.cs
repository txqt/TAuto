using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

namespace TAuto.Core;

/// <summary>
/// Manages screen capture caching and retrieval (Pull-based).
/// Thread-safe: captures can be read from UI thread while updated from background.
/// Anti-Freeze: captures are wrapped in a timeout to prevent hanging if the target window freezes.
/// </summary>
public class ScreenCaptureManager : IDisposable
{
    private readonly IDeviceController _device;
    private readonly object _lock = new();

    private IImage? _lastScreenCapture;
    private DateTime? _lastCaptureTime;
    private int _consecutiveTimeouts = 0;

    public IImage? LastScreenCapture
    {
        get { lock (_lock) { return _lastScreenCapture; } }
        private set
        {
            lock (_lock)
            {
                if (_lastScreenCapture != value)
                {
                    _lastScreenCapture?.Dispose();
                }
                _lastScreenCapture = value;
            }
        }
    }

    public DateTime? LastCaptureTime
    {
        get { lock (_lock) { return _lastCaptureTime; } }
        private set { lock (_lock) { _lastCaptureTime = value; } }
    }

    public int CaptureIntervalMs { get; set; } = 100;

    /// <summary>
    /// Minimum interval (ms) between captures, even when forced.
    /// Prevents redundant screenshots when multiple templates are checked in the same polling cycle.
    /// Default 50ms — screen can't meaningfully change faster than this.
    /// </summary>
    public int MinCaptureIntervalMs { get; set; } = 50;

    public System.Drawing.Point? LastFoundImageLocation { get; set; }

    /// <summary>
    /// Hard timeout (ms) for a single screen capture call.
    /// Prevents indefinite hanging if the target window is "Not Responding".
    /// Default 5000ms. Set lower for faster failure detection.
    /// </summary>
    public int CaptureTimeoutMs { get; set; } = 5000;

    public ScreenCaptureManager(IDeviceController device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>
    /// Requests an on-demand update of the screen capture.
    /// Returns true if a new capture was obtained or if the existing one is fresh enough.
    /// </summary>
    public async Task<bool> UpdateScreenCaptureAsync(bool force = false)
    {
        if (string.IsNullOrEmpty(_device.TargetId))
            return false;

        // Even forced captures respect minimum interval — screen can't change in <50ms
        if (LastCaptureTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - LastCaptureTime.Value).TotalMilliseconds;
            if (elapsed < MinCaptureIntervalMs)
                return LastScreenCapture != null;
        }

        if (!force && LastScreenCapture != null && LastCaptureTime.HasValue)
        {
            var elapsed = (DateTime.UtcNow - LastCaptureTime.Value).TotalMilliseconds;
            if (elapsed < CaptureIntervalMs)
                return true;
        }

        return await DoCaptureInternalAsync();
    }

    private async Task<bool> DoCaptureInternalAsync()
    {
        try
        {
            var captureTask = _device.CaptureScreenAsync();

            IImage? capture;
            try
            {
                capture = await captureTask.WaitAsync(TimeSpan.FromMilliseconds(CaptureTimeoutMs));
            }
            catch (TimeoutException)
            {
                // AUDIT FIX: Dispose orphaned capture result.
                _ = captureTask.ContinueWith(async t =>
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        var img = await t;
                        img?.Dispose();
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion);

                _consecutiveTimeouts++;
                Debug.WriteLine($"[ScreenCapture] ⚠️ Capture timed out after {CaptureTimeoutMs}ms (consecutive: {_consecutiveTimeouts})");
                LastScreenCapture = null;
                LastCaptureTime = null;
                return false;
            }

            if (capture != null)
            {
                LastScreenCapture = capture; // Automatically disposes previous
                LastCaptureTime = DateTime.UtcNow;
                _consecutiveTimeouts = 0;
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScreenCapture] ❌ Capture failed: {ex.Message}");
            LastScreenCapture = null;
            LastCaptureTime = null;
        }

        return false;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _lastScreenCapture?.Dispose();
            _lastScreenCapture = null;
        }
    }
}
