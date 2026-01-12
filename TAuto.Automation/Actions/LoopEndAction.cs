using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Ends a loop block and jumps back to start.
/// </summary>
public class LoopEndAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"â†©ï¸ End Loop";
    
    public string LoopId { get; set; } = "";

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(LoopId))
            return Task.FromResult(ActionResult.Fail("Loop ID not set"));
            
        // Jump back to the matching StartAction
        return Task.FromResult(ActionResult.Jump($"LOOP_REPEAT:{LoopId}"));
    }
}
