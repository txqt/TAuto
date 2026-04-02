using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

namespace TAuto.Core;

/// <summary>
/// Manages screen capture caching and retrieval.
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

    private CancellationTokenSource? _captureLoopCts;
    private Task? _captureLoopTask;

    /// <summary>
    /// Event fired whenever a new frame is successfully captured by the continuous loop.
    /// Used by pipelines (e.g. VisionPipeline) for a push-based model.
    /// </summary>
    public event EventHandler<IImage>? FrameCaptured;

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
    /// Starts a continuous background capture loop emitting frames via FrameCaptured.
    /// Respects CaptureIntervalMs for pacing.
    /// </summary>
    public void StartCaptureLoop()
    {
        lock (_lock)
        {
            if (_captureLoopCts != null) return;
            _captureLoopCts = new CancellationTokenSource();
            _captureLoopTask = Task.Run(() => CaptureLoopAsync(_captureLoopCts.Token));
        }
    }

    /// <summary>
    /// Stops the continuous background capture loop.
    /// </summary>
    public void StopCaptureLoop()
    {
        lock (_lock)
        {
            if (_captureLoopCts == null) return;
            _captureLoopCts.Cancel();
            _captureLoopCts.Dispose();
            _captureLoopCts = null;
        }
    }

    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var interval = TimeSpan.FromMilliseconds(Math.Max(1, CaptureIntervalMs));
                using var timer = new PeriodicTimer(interval);

                while (await timer.WaitForNextTickAsync(ct))
                {
                    if (string.IsNullOrEmpty(_device.TargetId))
                        continue;

                    var success = await DoCaptureInternalAsync(ct);

                    // AUDIT FIX (CRITICAL-2): Transfer-of-ownership pattern for cloned frames.
                    // If FrameCaptured handler throws or no subscriber exists, the clone is
                    // disposed instead of leaked (~8MB per frame at 1080p).
                    if (success && LastScreenCapture != null)
                    {
                        IImage? frameClone = null;
                        try
                        {
                            frameClone = LastScreenCapture.Clone();
                            FrameCaptured?.Invoke(this, frameClone);
                            frameClone = null; // Ownership transferred to subscriber
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ScreenCaptureManager] FrameCaptured handler error: {ex.Message}");
                        }
                        finally
                        {
                            frameClone?.Dispose(); // Dispose only if NOT consumed
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScreenCaptureManager] Loop error: {ex.Message}");
                await Task.Delay(1000, ct); // Cool down on error
            }
        }
    }

    /// <summary>
    /// Legacy compatibility API: Requests an on-demand update of the screen capture.
    /// If the background loop is running, this will just return true if the current frame is fresh enough.
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

        return await DoCaptureInternalAsync(CancellationToken.None);
    }

    // AUDIT FIX (P0-2): Enforce capture timeout using Task.WhenAny.
    // The original code created a CTS but never passed it to CaptureScreenAsync(),
    // so a hung window ("Not Responding") would block the capture loop forever.
    private async Task<bool> DoCaptureInternalAsync(CancellationToken loopCt)
    {
        try
        {
            var captureTask = _device.CaptureScreenAsync();

            // optimized timeout enforcement using WaitAsync (.NET 6+)
            // reduces allocations compared to Task.WhenAny + Task.Delay
            IImage? capture;
            try
            {
                capture = await captureTask.WaitAsync(TimeSpan.FromMilliseconds(CaptureTimeoutMs), loopCt);
            }
            catch (TimeoutException)
            {
                // AUDIT FIX (CRITICAL-5): Dispose orphaned capture result.
                // The captureTask is orphaned after timeout, but it will eventually complete
                // and produce an IImage that nobody consumes. Attach a continuation to
                // dispose it, preventing ~8MB leak per timeout event.
                _ = captureTask.ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                        t.Result?.Dispose();
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
        catch (OperationCanceledException)
        {
            if (!loopCt.IsCancellationRequested)
            {
                _consecutiveTimeouts++;
                Debug.WriteLine($"[ScreenCapture] ⚠️ Capture canceled/timeout (consecutive: {_consecutiveTimeouts})");
                LastScreenCapture = null;
                LastCaptureTime = null;
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
        StopCaptureLoop();

        lock (_lock)
        {
            _lastScreenCapture?.Dispose();
            _lastScreenCapture = null;
        }
    }
}

