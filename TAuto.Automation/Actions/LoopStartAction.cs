using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Starts a counted loop.
/// </summary>
public class LoopStartAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"ðŸ” Loop {LoopCount}x ({CounterVariableName})";
    
    public int LoopCount { get; set; } = 1;
    public string CounterVariableName { get; set; } = "i";
    public string LoopId { get; set; } = Guid.NewGuid().ToString();

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        // Internal tracking variable unique to this LoopId
        string varName = $"_sys_loop_{LoopId}"; 
        
        int currentIter = context.GetInt(varName, 0);
        
        if (currentIter >= LoopCount)
        {
            // Reset for next run
            context.SetVariable(varName, 0);
            
            // Exit Loop: Jump to the matching EndAction + 1
            return Task.FromResult(ActionResult.Jump($"LOOP_EXIT:{LoopId}"));
        }
        
        // Expose user-facing counter (0-based)
        context.SetVariable(CounterVariableName, currentIter);
        
        // Increment internal counter
        context.SetVariable(varName, currentIter + 1);
        
        return Task.FromResult(ActionResult.Ok());
    }
}
