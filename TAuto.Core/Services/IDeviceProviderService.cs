using TAuto.Core.Models;

namespace TAuto.Core.Services;

/// <summary>
/// Service for discovering and providing device controllers.
/// Implementations: AdbDeviceProvider (Android), WindowsDeviceProvider (Windows Desktop)
/// </summary>
public interface IDeviceProviderService
{
    /// <summary>
    /// Human-readable name for this provider (e.g., "Android (ADB)", "Windows Desktop")
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Discover all available devices/targets for this platform.
    /// </summary>
    Task<List<DeviceInfo>> GetAvailableDevicesAsync();
    
    /// <summary>
    /// Create a device controller for the specified target.
    /// </summary>
    /// <param name="targetId">Device serial (Android) or Window Handle (Windows)</param>
    IDeviceController CreateController(string targetId);
    
    /// <summary>
    /// Checks if a targeted device is currently online and available to accept commands.
    /// </summary>
    Task<bool> IsDeviceOnlineAsync(string targetId);
}
