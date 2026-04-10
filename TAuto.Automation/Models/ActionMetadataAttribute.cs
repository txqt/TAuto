using System;

namespace TAuto.Automation.Models;

/// <summary>
/// Attribute applied to classes implementing IAction to provide metadata
/// for the Bot Editor (UI rendering, toolboxes, safety hints).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ActionMetadataAttribute : Attribute
{
    public string DisplayName { get; }
    public string Category { get; }
    public string Icon { get; }
    public string Description { get; set; }
    public ActionSafetyLevel SafetyLevel { get; set; } = ActionSafetyLevel.Safe;
    public bool IsAdvanced { get; set; }

    public ActionMetadataAttribute(string displayName, string category, string icon = "*")
    {
        DisplayName = displayName;
        Category = category;
        Icon = icon;
        Description = string.Empty;
    }
}

public enum ActionSafetyLevel
{
    Safe,
    PromptRequired,
    AdminOnly
}
