namespace TAuto.Shared.Models;

public record ReconciliationData(
    string RunId,
    string WorkerId,
    string BotName,
    string DeviceSerial,
    string Platform,
    DateTime StartTimeUtc,
    string? PipeName,
    string? BotFolder,
    int? ProcessId
);
