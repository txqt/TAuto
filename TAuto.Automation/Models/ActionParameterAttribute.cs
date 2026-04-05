using System;

namespace TAuto.Automation.Models;

/// <summary>
/// Attribute applied to properties inside an IAction to instruct the Bot Editor
/// on how to dynamically render the Property Inspector fields.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class ActionParameterAttribute : Attribute
{
    public string DisplayName { get; }
    public string Description { get; }
    
    /// <summary>
    /// Logical grouping of parameters in the inspector (e.g. "Vision Settings", "Advanced").
    /// </summary>
    public string Group { get; set; } = "General";
    
    /// <summary>
    /// If true, this parameter is only shown when the user toggles "Advanced Mode".
    /// Useful for progressive disclosure in the UI.
    /// </summary>
    public bool IsAdvanced { get; set; } = false;

    public ActionParameterAttribute(string displayName, string description = "")
    {
        DisplayName = displayName;
        Description = description;
    }
}
