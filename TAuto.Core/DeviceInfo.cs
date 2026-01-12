namespace TAuto.Core;

/// <summary>
/// Information about a connected device.
/// </summary>
public class DeviceInfo
{
    public string Serial { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    
    public string DisplayName => string.IsNullOrEmpty(Model) ? Serial : $"{Model} ({Serial})";
    
    public override string ToString() => DisplayName;
}
