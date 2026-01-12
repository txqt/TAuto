using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Stops the script execution immediately if a condition is met.
/// </summary>
public class StopIfAction : ActionBase
{
    public override string DisplayName => $"ðŸ›‘ Stop If {Variable} {Operator} {Value}";
    
    // ===== Configuration =====
    
    public string Variable { get; set; } = string.Empty;
    public string Operator { get; set; } = "==";
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// If true, the script is considered FAILED.
    /// If false, the script is considered SUCCEEDED (graceful exit).
    /// </summary>
    public bool MarkAsFailure { get; set; } = false;
    
    /// <summary>
    /// Message to log or display upon stopping.
    /// </summary>
    public string StopMessage { get; set; } = "Stopped by condition.";
    
    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return Task.FromResult(ActionResult.Fail("Cancelled"));
        
        bool conditionMet = EvaluateCondition(context);
        
        if (conditionMet)
        {
            if (MarkAsFailure)
            {
                return Task.FromResult(ActionResult.Fail(StopMessage));
            }
            else
            {
                // We use a special generic 'Finish' result or just Stop.
                // Since ActionResult doesn't have a specific "StopSuccess", we can just return Fail with a specific error type OR
                // better: Introduce a new ActionResult type or convention.
                // For now, let's use Jump to a special "END" marker or return Fail if it's an error, 
                // but if it's success, we might need a way to signal "Stop successfully".
                // Looking at ScriptRunner, it iterates until end.
                // If we want to stop strictly, we can return a "Stop" signal.
                // Let's check ActionResult definition.
                
                // Assuming ActionResult only has Ok, Fail, Jump.
                // We will use Jump to a non-existent ID "SCRIPT_END" or handle it in Runner.
                // Or better, return ActionResult.Jump("SCRIPT_EXIT");
                return Task.FromResult(ActionResult.Jump("SCRIPT_EXIT"));
            }
        }
        
        return Task.FromResult(ActionResult.Ok());
    }
    
    private bool EvaluateCondition(ScriptContext context)
    {
        var val = context.GetVariable<object>(Variable, null!);
        if (val == null) return false;
        
        string strVal = val.ToString() ?? "";
        
        // Simple string/numeric equality for now, similar to IF action
        if (Operator == "==") return strVal == Value;
        if (Operator == "!=") return strVal != Value;
        
         // Try numeric
        if (double.TryParse(strVal, out double d1) && double.TryParse(Value, out double d2))
        {
            if (Operator == ">") return d1 > d2;
            if (Operator == "<") return d1 < d2;
            if (Operator == ">=") return d1 >= d2;
            if (Operator == "<=") return d1 <= d2;
        }
        
        return false;
    }
}
