using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Simulates a key press (e.g., "Enter", "Space", "F", "Back").
/// </summary>
[ActionMetadata("Press Key", "Input & Gesture", "⌨️")]
public class PressKeyAction : ActionBase
{
    public override string DisplayName => $"⌨️ Press Key: {Key}";

    [ActionParameter("Key Name", "The key to press (e.g., Enter, Esc, AppSwitch).")]
    public string Key { get; set; } = string.Empty;

    [ActionParameter("Type Text", "Optional text to type instead of a single key.")]
    public string Text { get; set; } = string.Empty;

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        if (string.IsNullOrEmpty(context.TargetId)) return ActionResult.Fail("No device connected");

        bool success;
        
        if (!string.IsNullOrEmpty(Text))
        {
            success = await context.Device.SendTextAsync(Text);
        }
        else
        {
            if (string.IsNullOrEmpty(Key)) return ActionResult.Fail("Key is empty");
            success = await context.Device.SendKeyAsync(Key);
        }

        return success ? ActionResult.Ok() : ActionResult.Fail(string.IsNullOrEmpty(Text) ? $"Failed to press {Key}" : "Failed to send text");
    }
}
