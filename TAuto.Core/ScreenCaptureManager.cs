using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Manages screen capture caching and retrieval.
/// </summary>
public class ScreenCaptureManager
{
    private readonly IDeviceController _device;
    
    public BitmapSource? LastScreenCapture { get; private set; }
    public DateTime? LastCaptureTime { get; private set; }
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
