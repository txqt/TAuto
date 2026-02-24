namespace TAuto.Core;

/// <summary>
/// Global default configuration values and magic numbers for the automation engine.
/// Standardizes timeouts, intervals, and retry counts across the ecosystem.
/// </summary>
public static class AutomationDefaults
{
    // Screen / Display Defaults
    public const int DefaultScreenWidth = 1280;
    public const int DefaultScreenHeight = 720;
    
    // Capture & Vision Defaults
    public const int DefaultCaptureIntervalMs = 500;
    public const int DefaultMatchTimeoutMs = 10000;
    public const double DefaultMatchConfidence = 0.90;

    // Process Manager / Worker Lifecycle
    public const int DefaultWorkerRestartDelayMs = 5000;
    public const int DefaultWorkerShutdownTimeoutMs = 5000;
    public const int DefaultMaxCrashesBeforeStop = 5;
    public const int DefaultCrashWindowSeconds = 60;
    public const int DefaultWorkerConnectTimeoutMs = 10000;

    // State Machine Defaults
    public const int DefaultStateFastCheckIntervalMs = 50;
    public const int DefaultStateSlowCheckIntervalMs = 500;
    public const int DefaultStateSlowdownThreshold = 3;
    public const int DefaultMaxTransitions = 1000;
}
