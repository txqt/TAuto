using System.Collections.Generic;

namespace TAuto.Automation.BotSystem;

/// <summary>
/// Declarative configuration for a bot.
/// Returned by BotBase.GetConfiguration() to describe the bot's
/// run mode, arguments, and metadata.
/// </summary>
public class BotConfiguration
{
    /// <summary>
    /// Display name of the bot.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Short description of what the bot does.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// How the bot should be presented when it starts.
    /// </summary>
    public BotRunMode RunMode { get; set; } = BotRunMode.Standard;

    /// <summary>
    /// List of user-configurable arguments.
    /// </summary>
    public List<BotArgument> Arguments { get; set; } = new();
}
