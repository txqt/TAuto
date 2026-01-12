using System;
using System.Collections.Generic;
using TAuto.Core;

namespace TAuto.Automation;

/// <summary>
/// Represents a script containing a list of actions for the new automation framework.
/// This is the new format that supports polymorphic actions (IAction).
/// </summary>
public class AutomationScript
{
    /// <summary>
    /// Unique name for this script.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this script does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Name of the game/app this script is for.
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the script format (for future migrations).
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// List of actions to execute.
    /// </summary>
    public List<IAction> Actions { get; set; } = new();

    /// <summary>
    /// Variables to initialize when script starts.
    /// </summary>
    public Dictionary<string, object> InitialVariables { get; set; } = new();

    /// <summary>
    /// When this script was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When this script was last modified.
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Author of the script (optional).
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Screen resolution when script was created.
    /// </summary>
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }

    public override string ToString()
    {
        return $"{Name} ({Actions.Count} actions)";
    }
}
