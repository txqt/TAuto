using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using TAuto.Core;
using TAuto.Automation.Actions;

namespace TAuto.Automation;

/// <summary>
/// Custom JSON converter for IAction interface to support polymorphic serialization.
/// Adds a "$type" discriminator to identify the concrete action type.
/// </summary>
public class ActionJsonConverter : JsonConverter<IAction>
{
    private const string TypeDiscriminator = "$type";
    
    // Type mapping: short name -> full type
    private static readonly Dictionary<string, Type> TypeMap = new()
    {
        // Input actions
        ["tap"] = typeof(TapAction),
        ["swipe"] = typeof(SwipeAction),
        ["delay"] = typeof(DelayAction),
        
        // Vision actions
        ["findImage"] = typeof(FindImageAction),
        ["clickImage"] = typeof(ClickImageAction),
        ["waitForImage"] = typeof(WaitForImageAction),
        
        // Logic actions
        ["ifImageFound"] = typeof(IfImageFoundAction),
        ["ifVariable"] = typeof(IfVariableAction),
        ["goto"] = typeof(GotoAction),
        ["setVariable"] = typeof(SetVariableAction),
        
        // OCR actions
        ["ifTextFound"] = typeof(IfTextFoundAction),
        ["clickText"] = typeof(ClickTextAction),

        // Loop & Control Flow actions
        ["while"] = typeof(WhileAction),
        ["loopStart"] = typeof(LoopStartAction),
        ["loopEnd"] = typeof(LoopEndAction),
        ["stopIf"] = typeof(StopIfAction),

        // Utility actions
        ["log"] = typeof(LogAction),
        ["setRandomVariable"] = typeof(SetRandomVariableAction),
        
        // State Machine
        ["stateMachine"] = typeof(StateMachine.StateMachineAction),
    };
    
    // Reverse mapping: type -> short name
    private static readonly Dictionary<Type, string> ReverseTypeMap;
    
    static ActionJsonConverter()
    {
        ReverseTypeMap = TypeMap.ToDictionary(x => x.Value, x => x.Key);
    }
    
    public override IAction? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token");
        
        // Clone reader to peek at the type discriminator
        var readerClone = reader;
        
        string? typeName = null;
        
        // Find the type discriminator
        while (readerClone.Read())
        {
            if (readerClone.TokenType == JsonTokenType.PropertyName)
            {
                string? propertyName = readerClone.GetString();
                readerClone.Read();
                
                if (propertyName == TypeDiscriminator)
                {
                    typeName = readerClone.GetString();
                    break;
                }
            }
            
            if (readerClone.TokenType == JsonTokenType.EndObject)
                break;
        }
        
        if (string.IsNullOrEmpty(typeName))
            throw new JsonException($"Missing '{TypeDiscriminator}' property for action deserialization");
        
        if (!TypeMap.TryGetValue(typeName, out Type? actionType))
            throw new JsonException($"Unknown action type: {typeName}");
        
        // Deserialize using the concrete type
        // We need to use a new options without this converter to avoid infinite recursion
        var newOptions = new JsonSerializerOptions(options);
        newOptions.Converters.Clear();
        foreach (var converter in options.Converters)
        {
            if (converter is not ActionJsonConverter)
                newOptions.Converters.Add(converter);
        }
        
        // Read the full object using the appropriate type
        using var doc = JsonDocument.ParseValue(ref reader);
        return (IAction?)JsonSerializer.Deserialize(doc.RootElement.GetRawText(), actionType, newOptions);
    }

    public override void Write(Utf8JsonWriter writer, IAction value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        
        if (!ReverseTypeMap.TryGetValue(type, out string? typeName))
            throw new JsonException($"Type not registered for serialization: {type.Name}");
        
        writer.WriteStartObject();
        
        // Write type discriminator first
        writer.WriteString(TypeDiscriminator, typeName);
        
        // Write all properties of the concrete type
        // Create options without this converter to avoid infinite recursion
        var newOptions = new JsonSerializerOptions(options);
        newOptions.Converters.Clear();
        foreach (var converter in options.Converters)
        {
            if (converter is not ActionJsonConverter)
                newOptions.Converters.Add(converter);
        }
        
        // Serialize to JsonDocument and copy properties
        using var doc = JsonSerializer.SerializeToDocument(value, type, newOptions);
        
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            property.WriteTo(writer);
        }
        
        writer.WriteEndObject();
    }
    
    /// <summary>
    /// Registers a new action type for serialization.
    /// Call this when adding new action types.
    /// </summary>
    public static void RegisterActionType<T>(string typeName) where T : IAction
    {
        TypeMap[typeName] = typeof(T);
        ReverseTypeMap[typeof(T)] = typeName;
    }
}
