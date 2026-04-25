namespace TAuto.Shared.Ipc;

/// <summary>
/// Centralized worker status constants.
/// All status producers (ProcessManagerService) and consumers (WorkerOrchestrator, WebUI)
/// should reference these constants instead of inline string literals.
/// </summary>
public static class WorkerStates
{
    public const string Starting = "starting";
    public const string Running = "running";
    public const string Paused = "paused";
    public const string Stopping = "stopping";
    public const string Stopped = "stopped";
    public const string Crashed = "crashed";
    public const string Restarting = "restarting";
    public const string RestartFailed = "restart_failed";
    public const string CrashLoopStopped = "crash_loop_stopped";
    public const string TimeoutReady = "TIMEOUT: ready signal";
    public const string StartTimeout = "TIMEOUT: start command";
    public const string HandlerError = "handler_error";
    public const string ZombieReaped = "zombie_reaped";
    public const string HardwareMissing = "hardware_missing";
    public const string PipeError = "pipe_error";
    public const string Initializing = "initializing";
    public const string ReadyPhase = "ready";
    public const string Hung = "hung";  // P0-6: Worker not responding to heartbeat
    public const string ResponseTimeout = "response_timeout";  // P0-6: Heartbeat timeout detected
}
