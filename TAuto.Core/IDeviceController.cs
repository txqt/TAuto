using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Platform-independent device control interface.
/// Implementations: ADB (Android), WinAPI (Desktop), Selenium (Web)
/// </summary>
public interface IDeviceController
{
    /// <summary>
    /// Unique identifier for target (device serial, window handle, URL)
    /// </summary>
    string TargetId { get; set; }
    
    /// <summary>
    /// Screen dimensions of current target
    /// </summary>
    (int Width, int Height) ScreenSize { get; }
    
    /// <summary>
    /// Tap/Click at absolute coordinates
    /// </summary>
    Task<bool> TapAsync(int x, int y);
    
    /// <summary>
    /// Swipe/Drag from point A to point B
    /// </summary>
    Task<bool> SwipeAsync(int x1, int y1, int x2, int y2, int durationMs);
    
    /// <summary>
    /// Capture current screen/window
    /// </summary>
    Task<BitmapSource?> CaptureScreenAsync();
    
    /// <summary>
    /// Check if target is available/connected
    /// </summary>
    Task<bool> IsAvailableAsync();
}
