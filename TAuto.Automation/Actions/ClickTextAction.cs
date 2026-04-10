using TAuto.Core;
using TAuto.Automation.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that finds text on the screen and clicks it.
/// </summary>
[ActionMetadata("Click Text", "Vision & OCR", "🔤")]
public class ClickTextAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"🖱️ Text: {Name}"
        : $"🖱️ Click Text: \"{TargetText}\"";
    
    // ===== Configuration =====
    
    [ActionParameter("Name", "Friendly name for this action.")]
    public string Name { get; set; } = string.Empty;

    [ActionParameter("Target Text", "The text to find and click on screen.")]
    public string TargetText { get; set; } = string.Empty;

    [ActionParameter("Case Sensitive", "Whether to match upper/lower case strictly.", IsAdvanced = true)]
    public bool CaseSensitive { get; set; } = false;

    [ActionParameter("Partial Match", "Allow matching of sub-strings.", IsAdvanced = true)]
    public bool PartialMatch { get; set; } = true;

    [ActionParameter("Offset X", "Horizontal click offset.", IsAdvanced = true)]
    public int OffsetX { get; set; } = 0;

    [ActionParameter("Offset Y", "Vertical click offset.", IsAdvanced = true)]
    public int OffsetY { get; set; } = 0;
    
    private int _consecutiveFrames = 1;
    public int ConsecutiveFrames 
    { 
        get => _consecutiveFrames; 
        set => SetProperty(ref _consecutiveFrames, value); 
    }
    
    // ===== Execute =====
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");
            
        if (string.IsNullOrEmpty(TargetText))
            return ActionResult.Fail("Target text not set");
        
        var confirmation = new TAuto.Automation.Utilities.DetectionConfirmation(ConsecutiveFrames);
        OcrResultBlock? match = null;
        bool isConfirmed = false;
        
        int maxAttempts = ConsecutiveFrames == 1 ? 1 : ConsecutiveFrames + 5;
        for (int i = 0; i < maxAttempts; i++)
        {
            await context.UpdateScreenCaptureAsync(force: true);
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot capture screen");
            
            var blocks = context.Ocr.GetTextBlocks(context.LastScreenCapture);
            var m = blocks.FirstOrDefault(b => CheckMatch(b.Text));
            if (m != null) match = m;
            
            if (confirmation.RecordResult(m != null))
            {
                isConfirmed = true;
                break;
            }
            
            if (ConsecutiveFrames > 1 && !isConfirmed && i < maxAttempts - 1)
            {
                await Task.Delay(200, ct);
            }
        }
        
        if (!isConfirmed || match == null)
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
