namespace TAuto.Shared.Ipc;

/// <summary>
/// Startup configuration sent from Manager to Worker via the "start" IPC message.
/// Contains everything the Worker needs to initialize and run a bot.
/// </summary>
public class WorkerStartupArgs
{
    /// <summary>
    /// Unique identifier for this worker instance (e.g., "worker-3").
    /// Used in structured logging and metrics.
    /// </summary>
    public string WorkerId { get; set; } = string.Empty;

    /// <summary>
    /// The window handle (HWND) to target, as a string.
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// The Platform string (e.g "Android (ADB)" or "Windows Desktop")
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Fully qualified type name of the BotBase to load (e.g., "AutoBot.App.Bots.ChallengeBot").
    /// </summary>
    public string BotTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Path to the bot DLL if loading an external bot.
    /// Null/empty for built-in bots.
    /// </summary>
    public string? BotDllPath { get; set; }

    /// <summary>
    /// Bot arguments (key-value pairs from UI).
    /// </summary>
    public Dictionary<string, object> Arguments { get; set; } = new();

    /// <summary>
    /// Base directory for template images (resolved relative paths).
    /// </summary>
    public string? BaseDirectory { get; set; }

    /// <summary>
    /// The encryption key for bot payloads.
    /// </summary>
    public string? BundleKey { get; set; }

    /// <summary>
    /// The expected SHA256 hash of the payload file on disk, to prevent tampering.
    /// </summary>
    public string? ExpectedPayloadHash { get; set; }

    /// <summary>
    /// Preferred input mode for the worker (Background, Foreground, Hardware).
    /// </summary>
    public string? InputMode { get; set; }

    /// <summary>
    /// COM port for hardware input (if applicable).
    /// </summary>
    public string? ComPort { get; set; }

    /// <summary>
    /// When true, state machine transitions emit IPC trace events (higher I/O/CPU). Default off for production.
    /// </summary>
    public bool EnableTrace { get; set; }

    /// <summary>
    /// Path to a standalone Native AOT bot executable.
    /// When set, Engine spawns this EXE directly instead of Worker.exe.
    /// The bot code is compiled into the EXE — no Assembly.Load needed.
    /// </summary>
    public string? NativeExePath { get; set; }

    /// <summary>
    /// Named Pipe name for VisionServer communication.
    /// Native AOT bots use this to connect to a VisionServer process
    /// for OpenCV/Tesseract operations (since they can't link those directly).
    /// </summary>
    public string? VisionPipeName { get; set; }
}
