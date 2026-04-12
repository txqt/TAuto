namespace TAuto.Core.Models;

public enum DeviceInputMode
{
    Background, // Uses PostMessage/ADB (Non-intrusive)
    Foreground, // Uses SendInput (Requires focus, stealing mouse)
    Hardware    // Uses Serial COM port to external Hardware emulator (Stealth)
}
