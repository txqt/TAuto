using System.Reflection;
using TAuto.Core;

namespace TAuto.Automation.Models;

/// <summary>
/// Scans assemblies for IAction implementations and extracts editor metadata.
/// </summary>
public class ActionMetadataService
{
    private readonly Dictionary<string, ActionDefinition> _actionDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Type> _actionTypes = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, ActionDefinition> ActionDefinitions => _actionDefinitions;
    public IReadOnlyDictionary<string, Type> ActionTypes => _actionTypes;

    public void Initialize(params Assembly[] assembliesToScan)
    {
        _actionDefinitions.Clear();
        _actionTypes.Clear();

        var actionType = typeof(IAction);

        foreach (var assembly in assembliesToScan.Distinct())
        {
            var actionImplementations = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface && actionType.IsAssignableFrom(t));

            foreach (var implementation in actionImplementations)
            {
                var metadata = implementation.GetCustomAttribute<ActionMetadataAttribute>();
                if (metadata == null)
                {
                    continue;
                }

                var definition = new ActionDefinition
                {
                    TypeName = implementation.Name,
                    QualifiedTypeName = implementation.AssemblyQualifiedName ?? implementation.FullName ?? implementation.Name,
                    DisplayName = metadata.DisplayName,
                    Category = metadata.Category,
                    Icon = metadata.Icon,
                    Description = metadata.Description,
                    SafetyLevel = metadata.SafetyLevel,
                    Parameters = ExtractParameters(implementation)
                };

                _actionDefinitions[definition.TypeName] = definition;
                _actionTypes[definition.TypeName] = implementation;
                _actionTypes[definition.QualifiedTypeName] = implementation;
            }
        }
    }

    public IReadOnlyList<ActionDefinition> GetOrderedDefinitions()
    {
        return _actionDefinitions.Values
            .OrderBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IAction CreateInstance(string typeName)
    {
        if (!_actionTypes.TryGetValue(typeName, out var type))
        {
            throw new InvalidOperationException($"Action type '{typeName}' is not registered.");
        }

        if (Activator.CreateInstance(type) is not IAction action)
        {
            throw new InvalidOperationException($"Unable to create action '{typeName}'.");
        }

        return action;
    }

    private static List<ActionParameterDefinition> ExtractParameters(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => new
            {
                Property = property,
                Metadata = property.GetCustomAttribute<ActionParameterAttribute>()
            })
            .Where(x => x.Metadata != null)
            .Select(x => new ActionParameterDefinition
            {
                PropertyName = x.Property.Name,
                PropertyType = x.Property.PropertyType,
                DisplayName = x.Metadata!.DisplayName,
                Description = x.Metadata.Description,
                Group = x.Metadata.Group,
                IsAdvanced = x.Metadata.IsAdvanced,
                EditorType = x.Metadata.EditorType
            })
            .ToList();
    }
}

public class ActionDefinition
{
    public string TypeName { get; set; } = string.Empty;
    public string QualifiedTypeName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ActionSafetyLevel SafetyLevel { get; set; } = ActionSafetyLevel.Safe;
    public List<ActionParameterDefinition> Parameters { get; set; } = new();
}

public class ActionParameterDefinition
{
    public string PropertyName { get; set; } = string.Empty;
    public Type PropertyType { get; set; } = typeof(string);
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public bool IsAdvanced { get; set; }
    public ActionParameterEditorType EditorType { get; set; } = ActionParameterEditorType.Default;
}
