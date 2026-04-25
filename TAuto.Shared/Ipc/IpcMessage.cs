using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TAuto.Shared.Ipc;

/// <summary>
/// Base IPC message envelope exchanged between Manager <-> Worker via Named Pipes.
/// Wire format: UTF-8 JSON terminated by newline.
/// </summary>
public class IpcMessage
{
    private static readonly JsonSerializerOptions PayloadDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = IpcJsonSerializerContext.Default
    };

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Serialize to JSON string (no trailing newline — caller adds delimiter).
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, IpcJsonSerializerContext.Default.IpcMessage);

    /// <summary>
    /// Deserialize from JSON string.
    /// </summary>
    public static IpcMessage? FromJson(string json)
    {
        try
        {
            // Strip UTF-8 BOM if present (safety net for pipe encoding mismatches)
            if (json.Length > 0 && json[0] == '\uFEFF')
                json = json[1..];
            return JsonSerializer.Deserialize(json, IpcJsonSerializerContext.Default.IpcMessage);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[IPC] Message deserialization failed: {ex.Message}"); return null; }
    }

    /// <summary>
    /// Create a typed message with a payload object.
    /// </summary>
    public static IpcMessage Create<T>(string type, T payload)
    {
        if (payload is null)
            return new IpcMessage { Type = type };

        var payloadType = payload.GetType();
        JsonElement json;
        try
        {
            json = JsonSerializer.SerializeToElement(payload, payloadType, IpcJsonSerializerContext.Default);
        }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException(
                $"IPC payload type '{payloadType.FullName}' is not registered for AOT JSON serialization. " +
                "Add it to IpcJsonSerializerContext.",
                ex);
        }

        return new IpcMessage { Type = type, Payload = json };
    }

    /// <summary>
    /// Create a simple message with no payload.
    /// </summary>
    public static IpcMessage Create(string type)
        => new() { Type = type };

    /// <summary>
    /// Deserialize the payload to a specific type.
    /// </summary>
    public T? GetPayload<T>()
    {
        if (Payload == null) return default;
        var raw = Payload.Value.GetRawText();
        var targetType = typeof(T);

        JsonTypeInfo? typeInfo = null;
        try
        {
            typeInfo = IpcJsonSerializerContext.Default.GetTypeInfo(targetType);
        }
        catch
        {
            // fall through and try options-based deserialize
        }

        if (typeInfo != null)
        {
            return (T?)JsonSerializer.Deserialize(raw, typeInfo);
        }

        throw new NotSupportedException(
            $"IPC payload type '{targetType.FullName}' is not registered for AOT JSON serialization. " +
            "Add it to IpcJsonSerializerContext.");
    }
}
