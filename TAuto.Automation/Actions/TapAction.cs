using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that taps at a specific screen location.
/// </summary>
public class TapAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    
    public override string DisplayName => !string.IsNullOrEmpty(Name) 
        ? Name 
        : UsePercent 
            ? $"Tap ({XPercent:F1}%, {YPercent:F1}%)" 
            : $"Tap ({X}, {Y})";
    
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public double XPercent { get; set; }
    public double YPercent { get; set; }
    public bool UsePercent { get; set; }
    public int RandomOffset { get; set; } = 0;
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        if (string.IsNullOrEmpty(context.TargetId)) return ActionResult.Fail("No device connected");
        
        int tapX, tapY;
        
        if (UsePercent)
        {
            if (context.LastScreenCapture == null)
                await context.UpdateScreenCaptureAsync(force: true);
            
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot get screen dimensions");
            
            int screenWidth = context.LastScreenCapture.PixelWidth;
            int screenHeight = context.LastScreenCapture.PixelHeight;
            
            tapX = (int)(XPercent / 100.0 * screenWidth);
            tapY = (int)(YPercent / 100.0 * screenHeight);
        }
        else
        {
            tapX = X;
            tapY = Y;
        }
        
        if (RandomOffset > 0)
        {
            var rnd = new Random();
            tapX += rnd.Next(-RandomOffset, RandomOffset + 1);
            tapY += rnd.Next(-RandomOffset, RandomOffset + 1);
        }
        
        bool success = await context.Device.TapAsync(tapX, tapY);
        
        return success ? ActionResult.Ok() : ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");
    }
}
