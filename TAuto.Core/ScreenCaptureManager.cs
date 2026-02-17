using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Manages screen capture caching and retrieval.
/// Thread-safe: captures can be read from UI thread while updated from background.
/// Anti-Freeze: captures are wrapped in a timeout to prevent hanging if the target window freezes.
/// </summary>
public class ScreenCaptureManager
{
    private readonly IDeviceController _device;
    private readonly object _lock = new();
    
    private BitmapSource? _lastScreenCapture;
    private DateTime? _lastCaptureTime;
    private int _consecutiveTimeouts = 0;

    public BitmapSource? LastScreenCapture 
    { 
        get { lock (_lock) { return _lastScreenCapture; } }
        private set { lock (_lock) { _lastScreenCapture = value; } }
    }
    
    public DateTime? LastCaptureTime 
    { 
        get { lock (_lock) { return _lastCaptureTime; } }
        private set { lock (_lock) { _lastCaptureTime = value; } }
    }
    
    public int CaptureIntervalMs { get; set; } = 100;
    public System.Windows.Point? LastFoundImageLocation { get; set; }

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

    public async Task<bool> UpdateScreenCaptureAsync(bool force = false)
    {
        if (string.IsNullOrEmpty(_device.TargetId))
            return false;
            
        if (!force && LastScreenCapture != null && LastCaptureTime.HasValue)
        {
            var elapsed = (DateTime.Now - LastCaptureTime.Value).TotalMilliseconds;
            if (elapsed < CaptureIntervalMs)
                return true;
        }

        try
        {
            var captureTask = _device.CaptureScreenAsync();
            var timeoutTask = Task.Delay(CaptureTimeoutMs);
            var completedTask = await Task.WhenAny(captureTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _consecutiveTimeouts++;
                Debug.WriteLine($"[ScreenCapture] ⚠️ Capture timed out after {CaptureTimeoutMs}ms (consecutive: {_consecutiveTimeouts})");
                return false;
            }

            var capture = await captureTask;
            if (capture != null)
            {
                LastScreenCapture = capture;
                LastCaptureTime = DateTime.Now;
                _consecutiveTimeouts = 0;
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScreenCapture] ❌ Capture failed: {ex.Message}");
        }
        
        return false;
    }
}

