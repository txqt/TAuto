using System.Text.Json;
using System.Text.Json.Serialization;
using TAuto.Core;

namespace TAuto.Automation.Models;

/// <summary>
/// Polymorphic converter for IAction implementations used inside BotProfile JSON.
/// 
/// - Dev mode (default): Uses reflection to auto-discover action types.
/// - SaaS mode (SAAS_BUILD): Uses StaticActionFactory for AOT compatibility.
/// </summary>
public sealed class ActionJsonConverter : JsonConverter<IAction>
{
    private const string TypePropertyName = "type";
    private readonly Dictionary<string, Type> _typeMap;
    private readonly Dictionary<Type, string> _reverseTypeMap;

    public ActionJsonConverter()
    {
#if SAAS_BUILD
        // AOT-safe: Use the static factory (no reflection)
        _typeMap = new Dictionary<string, Type>(StringComparer.Ordinal);
        _reverseTypeMap = new Dictionary<Type, string>();

        foreach (var kvp in StaticActionFactory.GetAllFactories())
        {
            var instance = kvp.Value();
            var type = instance.GetType();
            _typeMap[kvp.Key] = type;
            _reverseTypeMap[type] = kvp.Key;
        }
#else
        // Dev mode: Reflection-based auto-discovery (unchanged from original)
        var actionTypes = typeof(ActionJsonConverter).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IAction).IsAssignableFrom(t))
            .ToList();

        _typeMap = new Dictionary<string, Type>(StringComparer.Ordinal);
        _reverseTypeMap = new Dictionary<Type, string>();

        foreach (var t in actionTypes)
        {
            var attr = (ActionTypeIdentifierAttribute?)Attribute.GetCustomAttribute(t, typeof(ActionTypeIdentifierAttribute));
            var identifier = attr?.Identifier ?? t.FullName ?? t.Name;

            _typeMap[identifier] = t;
            _reverseTypeMap[t] = identifier;
        }
#endif
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
        var identifier = _reverseTypeMap.TryGetValue(actionType, out var id) ? id : (actionType.FullName ?? actionType.Name);
        writer.WriteString(TypePropertyName, identifier);
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
