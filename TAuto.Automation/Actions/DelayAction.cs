using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that waits for a specified duration.
/// </summary>
public class DelayAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"⏱️ Delay {DelayMs}ms";
    
    public int DelayMs { get; set; } = 1000;
    public int RandomMs { get; set; } = 0;
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        
        int actualDelay = DelayMs;
        if (RandomMs > 0)
        {
            actualDelay += new Random().Next(-RandomMs, RandomMs + 1);
        }
        
        await Task.Delay(Math.Max(0, actualDelay), ct);
        return ActionResult.Ok();
    }
}
