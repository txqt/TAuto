using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Manages screen capture caching and retrieval.
/// Thread-safe: captures can be read from UI thread while updated from background.
/// </summary>
public class ScreenCaptureManager
{
    private readonly IDeviceController _device;
    private readonly object _lock = new();
    
    private BitmapSource? _lastScreenCapture;
    private DateTime? _lastCaptureTime;

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
        
        var capture = await _device.CaptureScreenAsync();
        if (capture != null)
        {
            LastScreenCapture = capture;
            LastCaptureTime = DateTime.Now;
            return true;
        }
        
        return false;
    }
}

