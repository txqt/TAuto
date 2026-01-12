using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Automation.Actions;

/// <summary>
/// Simple action that jumps to a specified action by ID.
/// Useful for unconditional branching and creating loops.
/// </summary>
public class GotoAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"↪️ Goto {TargetActionId}";
    
    // ===== Configuration =====
    
    /// <summary>
    /// The action ID to jump to.
    /// </summary>
    public string TargetActionId { get; set; } = string.Empty;
    
    // ===== Execute =====
    
    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ActionResult.Fail("Cancelled"));
        
        if (string.IsNullOrEmpty(TargetActionId))
            return Task.FromResult(ActionResult.Fail("Target action ID not set"));
        
        return Task.FromResult(ActionResult.Jump(TargetActionId));
    }
}
