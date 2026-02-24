using TAuto.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that finds text on the screen and clicks it.
/// </summary>
public class ClickTextAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"ðŸ–±ï¸ Text: {Name}"
        : $"ðŸ–±ï¸ Click Text: \"{TargetText}\"";
    
    // ===== Configuration =====
    
    public string Name { get; set; } = string.Empty;
    public string TargetText { get; set; } = string.Empty;
    public bool CaseSensitive { get; set; } = false;
    public bool PartialMatch { get; set; } = true;
    
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;
    
    // ===== Execute =====
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");
            
        if (string.IsNullOrEmpty(TargetText))
            return ActionResult.Fail("Target text not set");
        
        // Capture
        await context.UpdateScreenCaptureAsync(force: true);
        if (context.LastScreenCapture == null)
            return ActionResult.Fail("Cannot capture screen");
        
        // Scan blocks
        var blocks = context.Ocr.GetTextBlocks(context.LastScreenCapture);
        
        // Find matching block
        var match = blocks.FirstOrDefault(b => CheckMatch(b.Text));
        
        if (match == null)
            return ActionResult.Fail($"Text not found: {TargetText}");
            
        // Click center of text block
        int tapX = (int)match.Center.X + OffsetX;
        int tapY = (int)match.Center.Y + OffsetY;
        
        context.LastFoundImageLocation = new System.Drawing.Point(tapX, tapY);
        
        bool success = await context.Device.TapAsync(tapX, tapY);
        
        return success
            ? ActionResult.Ok(match.Center)
            : ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");
    }
    
    private bool CheckMatch(string source)
    {
        string target = TargetText;
        StringComparison comparison = CaseSensitive 
            ? StringComparison.Ordinal 
            : StringComparison.OrdinalIgnoreCase;
            
        if (PartialMatch)
        {
            return source.IndexOf(target, comparison) >= 0;
        }
        else
        {
            return source.Equals(target, comparison);
        }
    }
}
