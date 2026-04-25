namespace TAuto.Shared.Ipc;

/// <summary>
/// Heartbeat payload sent from Worker → Manager periodically.
/// </summary>
public class WorkerHeartbeat
{
    public string WorkerId { get; set; } = string.Empty;
    public string ProtocolVersion { get; set; } = "1.0";
    public string Status { get; set; } = "running";  // running, paused, idle, error
    public string? CurrentState { get; set; }
    public string StateClass { get; set; } = "idle";
    public string? TargetId { get; set; }
    public long MemoryBytes { get; set; }
    public double CpuUsagePercent { get; set; }
    public int TickCount { get; set; }
    public double UptimeSeconds { get; set; }
    public double AvgMatchTimeMs { get; set; }
    public double FrameAgeMs { get; set; }
    public double CaptureRate { get; set; }
    public int DroppedFrames { get; set; }
    public int ConsecutiveMisses { get; set; }
    public double AdbRoundTripMs { get; set; }
    public double LatencyMs { get; set; }
    public int RecoveryCount { get; set; }
    public string? RecoveryReasonCode { get; set; }
}
