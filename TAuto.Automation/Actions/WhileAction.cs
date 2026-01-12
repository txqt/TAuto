using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

public enum ComparisonOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    Contains
}

/// <summary>
/// Starts a conditional while loop.
/// </summary>
public class WhileAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"ðŸ”„ While {ConditionVariable} {Operator} {ConditionValue}";
    
    public string LoopId { get; set; } = Guid.NewGuid().ToString();
    
    public string ConditionVariable { get; set; } = "";
    public string ConditionValue { get; set; } = "";
    public ComparisonOperator Operator { get; set; } = ComparisonOperator.Equals;
    
    public int MaxIterations { get; set; } = 1000; // Protection against infinite loops

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        // 1. Check Infinite Loop Protection
        string counterVar = $"_sys_while_{LoopId}";
        int currentIter = context.GetInt(counterVar, 0);
        
        if (currentIter >= MaxIterations)
        {
            context.SetVariable(counterVar, 0); // Reset
            return Task.FromResult(ActionResult.Fail($"While Loop exceeded max iterations ({MaxIterations})."));
        }
        
        // 2. Evaluate Condition
        bool conditionMet = CheckCondition(context);
        
        if (!conditionMet)
        {
            context.SetVariable(counterVar, 0); // Reset
            return Task.FromResult(ActionResult.Jump($"LOOP_EXIT:{LoopId}"));
        }
        
        // 3. Increment Safety Counter
        context.SetVariable(counterVar, currentIter + 1);
        
        return Task.FromResult(ActionResult.Ok());
    }
    
    private bool CheckCondition(ScriptContext context)
    {
        // Get variable value as string for generic comparison implementation
        // For numeric comparisons, we try parsing.
        string varVal = context.GetString(ConditionVariable, "");
        string targetVal = ConditionValue;
        
        // Use double for numeric comparison if possible
        double numVar = 0, numTarget = 0;
        bool isNumeric = double.TryParse(varVal, out numVar) && 
                         double.TryParse(targetVal, out numTarget);
                         
        switch (Operator)
        {
            case ComparisonOperator.Equals:
                return varVal.Equals(targetVal, StringComparison.OrdinalIgnoreCase);
                
            case ComparisonOperator.NotEquals:
                return !varVal.Equals(targetVal, StringComparison.OrdinalIgnoreCase);
                
            case ComparisonOperator.Contains:
                return varVal.IndexOf(targetVal, StringComparison.OrdinalIgnoreCase) >= 0;
                
            case ComparisonOperator.GreaterThan:
                return isNumeric && numVar > numTarget;
                
            case ComparisonOperator.LessThan:
                return isNumeric && numVar < numTarget;
                
            default:
                return false;
        }
    }
}
