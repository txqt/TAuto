using System;

namespace TAuto.Automation;

/// <summary>
/// Represents a single click/tap or swipe action that can be recorded and played back.
/// </summary>
public class ClickAction
{
    /// <summary>
    /// Type of action: Tap or Swipe
    /// </summary>
    public ActionType Type { get; set; } = ActionType.Tap;

    /// <summary>
    /// X coordinate in pixels (start point for swipe)
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Y coordinate in pixels (start point for swipe)
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// End X coordinate in pixels (for swipe only)
    /// </summary>
    public int EndX { get; set; }

    /// <summary>
    /// End Y coordinate in pixels (for swipe only)
    /// </summary>
    public int EndY { get; set; }

    /// <summary>
    /// X coordinate as percentage of screen width (0-100)
    /// </summary>
    public double XPercent { get; set; }

    /// <summary>
    /// Y coordinate as percentage of screen height (0-100)
    /// </summary>
    public double YPercent { get; set; }

    /// <summary>
    /// End X coordinate as percentage (for swipe only)
    /// </summary>
    public double EndXPercent { get; set; }

    /// <summary>
    /// End Y coordinate as percentage (for swipe only)
    /// </summary>
    public double EndYPercent { get; set; }

    /// <summary>
    /// Duration of swipe in milliseconds (for swipe only)
    /// </summary>
    public int SwipeDuration { get; set; } = 300;

    /// <summary>
    /// Delay in milliseconds to wait after this action
    /// </summary>
    public int DelayAfter { get; set; } = 500;

    /// <summary>
    /// When this action was recorded
    /// </summary>
    public DateTime RecordedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Optional name/description for this action
    /// </summary>
    public string ActionName { get; set; } = string.Empty;

    /// <summary>
    /// Order index of this action in the sequence
    /// </summary>
    public int Order { get; set; }

    // ===== ImageDetect Properties =====
    
    /// <summary>
    /// Path to the template image file (for ImageDetect only)
    /// </summary>
    public string? TemplatePath { get; set; }

    /// <summary>
    /// Template name for display (for ImageDetect only)
    /// </summary>
    public string? TemplateName { get; set; }

    /// <summary>
    /// Matching threshold 0.0 - 1.0 (for ImageDetect only)
    /// </summary>
    public double MatchThreshold { get; set; } = 0.8;

    /// <summary>
    /// Timeout in ms to wait for image to appear (for ImageDetect only)
    /// </summary>
    public int DetectTimeout { get; set; } = 5000;

    /// <summary>
    /// Retry interval in ms (for ImageDetect only)
    /// </summary>
    public int RetryInterval { get; set; } = 500;

    /// <summary>
    /// Click offset X from center of found image (for ImageDetect only)
    /// </summary>
    public int ClickOffsetX { get; set; } = 0;

    /// <summary>
    /// Click offset Y from center of found image (for ImageDetect only)
    /// </summary>
    public int ClickOffsetY { get; set; } = 0;

    public override string ToString()
    {
        if (Type == ActionType.Swipe)
        {
            string name = string.IsNullOrEmpty(ActionName) 
                ? $"Swipe ({X},{Y}) ? ({EndX},{EndY})" 
                : ActionName;
            return $"{Order + 1}. ?? {name}";
        }
        else if (Type == ActionType.ImageDetect)
        {
            string name = string.IsNullOrEmpty(TemplateName) 
                ? "Find Image" 
                : TemplateName;
            return $"{Order + 1}. ?? {name} [{MatchThreshold:P0}]";
        }
        else
        {
            string name = string.IsNullOrEmpty(ActionName) 
                ? $"Tap ({X}, {Y})" 
                : ActionName;
            return $"{Order + 1}. ?? {name} [{XPercent:F1}%, {YPercent:F1}%]";
        }
    }
}

/// <summary>
/// Types of actions that can be recorded
/// </summary>
public enum ActionType
{
    Tap,
    Swipe,
    ImageDetect
}
