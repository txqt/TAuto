namespace TAuto.Automation.BotSystem;

/// <summary>
/// Defines how the bot's UI should be presented when it runs.
/// </summary>
public enum BotRunMode
{
    /// <summary>
    /// No custom UI. Bot runs embedded in the host's log panel.
    /// </summary>
    Standard,

    /// <summary>
    /// Bot defines its own WPF Window/UserControl that pops up on Start.
    /// </summary>
    CustomUI,

    /// <summary>
    /// A console window is allocated for the bot on Start.
    /// </summary>
    CLI
}
