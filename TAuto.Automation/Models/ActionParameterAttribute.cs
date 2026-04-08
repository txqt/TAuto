using System;

namespace TAuto.Automation.Models;

/// <summary>
/// Defines the type of UI editor to be used for an action parameter.
/// </summary>
public enum ActionParameterEditorType
{
    /// <summary>
    /// Default editor based on property type (TextBox, NumericUpDown, CheckBox).
    /// </summary>
    Default,
    
    /// <summary>
    /// File picker for image templates.
    /// </summary>
    ImagePath,
    
    /// <summary>
    /// Dropdown list of action IDs or scenario names.
    /// </summary>
    ActionId,
    
    /// <summary>
    /// Visual picker for X coordinate.
    /// </summary>
    CoordinateX,
    
    /// <summary>
    /// Visual picker for Y coordinate.
    /// </summary>
    CoordinateY,
    
    /// <summary>
    /// Visual box picker for a rectangular region (X, Y, W, H).
    /// </summary>
    Region,
    
    /// <summary>
    /// Dropdown list for a fixed set of choices.
    /// </summary>
    Choice
}

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

    /// <summary>
    /// Hint for the UI on which specialized editor to use.
    /// </summary>
    public ActionParameterEditorType EditorType { get; set; } = ActionParameterEditorType.Default;

    public ActionParameterAttribute(string displayName, string description = "")
    {
        DisplayName = displayName;
        Description = description;
    }
}
