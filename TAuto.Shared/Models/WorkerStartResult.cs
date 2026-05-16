namespace TAuto.Shared.Models;

public record WorkerStartResult(
    string WorkerId,
    int ProcessId,
    string? PipeName
);
