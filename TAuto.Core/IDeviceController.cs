using System.Threading.Tasks;
using TAuto.Core.Imaging;
using TAuto.Core.Models;

namespace TAuto.Core;

/// <summary>
/// Platform-independent device control interface.
/// Implementations: ADB (Android), WinAPI (Desktop), Selenium (Web)
/// </summary>
public interface ITouchInputDevice
{
    Task<bool> TapAsync(int x, int y);
    Task<bool> SwipeAsync(int x1, int y1, int x2, int y2, int durationMs);
    Task<bool> LongPressAsync(int x, int y, int durationMs);
}

public interface IKeyboardInputDevice
{
    Task<bool> SendKeyAsync(string key);
    Task<bool> SendTextAsync(string text);
}

public interface IScreenCaptureDevice
{
    Task<IImage?> CaptureScreenAsync();
}

public interface IAppLifecycleDevice
{
    Task<bool> LaunchAppAsync(string packageOrPath) => Task.FromResult(false);
    Task<bool> ForceStopAppAsync(string packageOrName) => Task.FromResult(false);
}

public interface IDeviceController : ITouchInputDevice, IKeyboardInputDevice, IScreenCaptureDevice, IAppLifecycleDevice
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
    /// Check if target is available/connected
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// The last measured communication latency in milliseconds (e.g., ADB round-trip).
    /// </summary>
    double LastCommunicationLatencyMs { get; }
}
