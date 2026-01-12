using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that swipes from one point to another.
/// </summary>
public class SwipeAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? Name
        : UsePercent
            ? $"Swipe ({StartXPercent:F0}%,{StartYPercent:F0}%) → ({EndXPercent:F0}%,{EndYPercent:F0}%)"
            : $"Swipe ({StartX},{StartY}) → ({EndX},{EndY})";
    
    public string Name { get; set; } = string.Empty;
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int EndX { get; set; }
    public int EndY { get; set; }
    public double StartXPercent { get; set; }
    public double StartYPercent { get; set; }
    public double EndXPercent { get; set; }
    public double EndYPercent { get; set; }
    public bool UsePercent { get; set; }
    public int DurationMs { get; set; } = 300;
    public int RandomOffset { get; set; } = 0;
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        if (string.IsNullOrEmpty(context.TargetId)) return ActionResult.Fail("No device connected");
        
        int x1, y1, x2, y2;
        
        if (UsePercent)
        {
            if (context.LastScreenCapture == null)
                await context.UpdateScreenCaptureAsync(force: true);
            
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot get screen dimensions");
            
            int screenWidth = context.LastScreenCapture.PixelWidth;
            int screenHeight = context.LastScreenCapture.PixelHeight;
            
            x1 = (int)(StartXPercent / 100.0 * screenWidth);
            y1 = (int)(StartYPercent / 100.0 * screenHeight);
            x2 = (int)(EndXPercent / 100.0 * screenWidth);
            y2 = (int)(EndYPercent / 100.0 * screenHeight);
        }
        else
        {
            x1 = StartX; y1 = StartY; x2 = EndX; y2 = EndY;
        }
        
        if (RandomOffset > 0)
        {
            var rnd = new Random();
            x1 += rnd.Next(-RandomOffset, RandomOffset + 1);
            y1 += rnd.Next(-RandomOffset, RandomOffset + 1);
            x2 += rnd.Next(-RandomOffset, RandomOffset + 1);
            y2 += rnd.Next(-RandomOffset, RandomOffset + 1);
        }
        
        bool success = await context.Device.SwipeAsync(x1, y1, x2, y2, DurationMs);
        
        return success ? ActionResult.Ok() : ActionResult.Fail($"Swipe failed from ({x1},{y1}) to ({x2},{y2})");
    }
}
