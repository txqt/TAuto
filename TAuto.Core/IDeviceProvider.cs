using TAuto.Core.Imaging;

namespace TAuto.Core;

/// <summary>
/// Interface for providing device and service access to UserControls.
/// </summary>
public interface IDeviceProvider
{
    /// <summary>
    /// Currently selected device.
    /// </summary>
    DeviceInfo? SelectedDevice { get; }
    
    /// <summary>
    /// Device controller for input actions.
    /// </summary>
    IDeviceController? DeviceController { get; }
    
    /// <summary>
    /// Vision service for template matching.
    /// </summary>
    IVisionService? VisionService { get; }
    
    /// <summary>
    /// Currently loaded/captured image.
    /// </summary>
    IImage? CurrentImage { get; }
}
