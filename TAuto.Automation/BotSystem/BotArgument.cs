using System;
using System.Collections.Generic;

namespace TAuto.Automation.BotSystem;

/// <summary>
/// Describes a single configurable argument for a bot.
/// Used by the host application to generate UI controls or CLI flags.
/// </summary>
public class BotArgument
{
    /// <summary>
    /// Internal key used to store/retrieve the value (e.g. "targetLevel").
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Human-readable label for the UI (e.g. "Target Level").
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// Description/tooltip shown in UI or CLI help text.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// The expected value type: String, Int, Bool, Double, or Choice.
    /// </summary>
    public BotArgumentType ArgumentType { get; set; } = BotArgumentType.String;

    /// <summary>
    /// Default value for this argument.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Available choices when ArgumentType is Choice (rendered as ComboBox or CLI enum).
    /// </summary>
    public List<string> Choices { get; set; } = new();
}

/// <summary>
/// Supported argument types for BotArgument.
/// </summary>
public enum BotArgumentType
{
    String,
    Int,
    Double,
    Bool,
    Choice
}
