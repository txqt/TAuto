namespace TAuto.Core.Models;

public enum DeviceInputMode
{
    Background, // Uses PostMessage/ABD (Non-intrusive)
    Foreground  // Uses SendInput (Requires focus, stealing mouse)
}
