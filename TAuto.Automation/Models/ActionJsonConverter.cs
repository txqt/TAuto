using System.Text.Json;
using System.Text.Json.Serialization;
using TAuto.Core;

namespace TAuto.Automation.Models;

/// <summary>
/// Polymorphic converter for IAction implementations used inside BotProfile JSON.
/// </summary>
public sealed class ActionJsonConverter : JsonConverter<IAction>
{
    private const string TypePropertyName = "type";
    private readonly Dictionary<string, Type> _typeMap;

    public ActionJsonConverter()
    {
        _typeMap = typeof(ActionJsonConverter).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IAction).IsAssignableFrom(t))
            .ToDictionary(t => t.FullName ?? t.Name, t => t, StringComparer.Ordinal);
    }

    public override IAction? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (!root.TryGetProperty(TypePropertyName, out var discriminator))
        {
            throw new JsonException("Missing action type discriminator.");
        }

        var typeName = discriminator.GetString();
        if (string.IsNullOrWhiteSpace(typeName) || !_typeMap.TryGetValue(typeName, out var actionType))
        {
            throw new JsonException($"Unknown action type '{typeName}'.");
        }

        var innerOptions = CreateInnerOptions(options);
        return (IAction?)JsonSerializer.Deserialize(root.GetRawText(), actionType, innerOptions);
    }

    public override void Write(Utf8JsonWriter writer, IAction value, JsonSerializerOptions options)
    {
        var actionType = value.GetType();
        var innerOptions = CreateInnerOptions(options);
        var serialized = JsonSerializer.SerializeToElement(value, actionType, innerOptions);

        writer.WriteStartObject();
        writer.WriteString(TypePropertyName, actionType.FullName ?? actionType.Name);
        foreach (var property in serialized.EnumerateObject())
        {
            property.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static JsonSerializerOptions CreateInnerOptions(JsonSerializerOptions options)
    {
        var clone = new JsonSerializerOptions(options);
        for (var i = clone.Converters.Count - 1; i >= 0; i--)
        {
            if (clone.Converters[i] is ActionJsonConverter)
            {
                clone.Converters.RemoveAt(i);
            }
        }

        return clone;
    }
}
