using System.Text.Json;
using System.Text.Json.Serialization;

namespace TAuto.Shared.Ipc;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    WriteIndented = false)]
[JsonSerializable(typeof(IpcMessage))]
[JsonSerializable(typeof(WorkerStartupArgs))]
[JsonSerializable(typeof(WorkerHeartbeat))]
[JsonSerializable(typeof(WorkerLogEntry))]
[JsonSerializable(typeof(WorkerTraceEntry))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal partial class IpcJsonSerializerContext : JsonSerializerContext
{
}
