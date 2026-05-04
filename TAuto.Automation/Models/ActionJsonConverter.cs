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
            
            // Register original key (usually FullName)
            _typeMap[kvp.Key] = type;
            
            // Also register short name (className) for AI compatibility
            _typeMap[type.Name] = type;
            
            _reverseTypeMap[type] = type.Name; // Prefer short name for writing
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
            
            // Primary identifier from attribute or short name
            var primaryId = attr?.Identifier ?? t.Name;
            
            _typeMap[primaryId] = t;
            
            // Also register FullName as fallback
            if (t.FullName != null && t.FullName != primaryId)
            {
                _typeMap[t.FullName] = t;
            }

            _reverseTypeMap[t] = primaryId;
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

        // Use source-generated TypeInfo for AOT safety
        var typeInfo = AutomationJsonContext.Default.GetTypeInfo(actionType);
        if (typeInfo == null)
        {
            throw new JsonException($"Missing AOT metadata for action type '{actionType.Name}'.");
        }

        return (IAction?)JsonSerializer.Deserialize(root.GetRawText(), typeInfo);
    }

    public override void Write(Utf8JsonWriter writer, IAction value, JsonSerializerOptions options)
    {
        var actionType = value.GetType();
        
        // Use source-generated TypeInfo for AOT safety
        var typeInfo = AutomationJsonContext.Default.GetTypeInfo(actionType);
        if (typeInfo == null)
        {
            throw new JsonException($"Missing AOT metadata for action type '{actionType.Name}'.");
        }

        var serialized = JsonSerializer.SerializeToElement(value, typeInfo);

        writer.WriteStartObject();
        var identifier = _reverseTypeMap.TryGetValue(actionType, out var id) ? id : (actionType.FullName ?? actionType.Name);
        writer.WriteString(TypePropertyName, identifier);
        foreach (var property in serialized.EnumerateObject())
        {
            if (property.NameEquals(TypePropertyName)) continue; // Skip if already present
            property.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
