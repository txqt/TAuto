using System;
using System.Collections.Generic;

namespace TAuto.Automation;

/// <summary>
/// Represents a saved game profile containing recorded actions and playback settings.
/// </summary>
public class GameProfile
{
    /// <summary>
    /// Unique name for this profile
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the game/app this profile is for
    /// </summary>
    public string GameName { get; set; } = string.Empty;

    /// <summary>
    /// List of recorded click actions
    /// </summary>
    public List<ClickAction> Actions { get; set; } = new();

    /// <summary>
    /// Number of times to loop the playback (1 = play once, 0 = infinite)
    /// </summary>
    public int LoopCount { get; set; } = 1;

    /// <summary>
    /// Delay in milliseconds between each loop iteration
    /// </summary>
    public int DelayBetweenLoops { get; set; } = 1000;

    /// <summary>
    /// Default delay between actions in milliseconds
    /// </summary>
    public int DefaultActionDelay { get; set; } = 500;

    /// <summary>
    /// When this profile was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When this profile was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Device serial this profile was created on (optional)
    /// </summary>
    public string? DeviceSerial { get; set; }

    /// <summary>
    /// Screen resolution when profile was created
    /// </summary>
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }

    public override string ToString()
    {
        return $"{ProfileName} ({Actions.Count} actions)";
    }
}
