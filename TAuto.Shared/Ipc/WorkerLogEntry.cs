namespace TAuto.Shared.Ipc;

/// <summary>
/// Log entry sent from Worker → Manager for aggregated display.
/// </summary>
public class WorkerLogEntry
{
    public string WorkerId { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO";  // INFO, WARN, ERROR, FATAL
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
