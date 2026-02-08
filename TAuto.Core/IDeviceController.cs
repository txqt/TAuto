using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TAuto.Core.Models;

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
    /// The current input mode of the device (e.g., touch, mouse).
    /// </summary>
    DeviceInputMode InputMode { get; set; }
    
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

    /// <summary>
    /// Send a key press event (e.g., Key.Enter, Key.Space, "A")
    /// </summary>
    Task<bool> SendKeyAsync(string key);

    /// <summary>
    /// Send text input (typing)
    /// </summary>
    Task<bool> SendTextAsync(string text);
}
