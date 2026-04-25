namespace TAuto.Shared.Ipc;

/// <summary>
/// Trace entry emitted by Worker for state machine execution.
/// </summary>
public class WorkerTraceEntry
{
    public string WorkerId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public string StateName { get; set; } = string.Empty;
    public string? TransitionTo { get; set; }
    public string? Details { get; set; }
    public int PollCount { get; set; }
    public double ElapsedMs { get; set; }
}

