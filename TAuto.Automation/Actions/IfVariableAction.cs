using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Conditional action that checks a variable value and branches accordingly.
/// </summary>
[ActionMetadata("If Variable", "Flow & Logic", "❓")]
public class IfVariableAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"❓ If: {VariableName} {Operator} {CompareValue}";
    
    // ===== Configuration =====
    
    [ActionParameter("Variable", "Name of the variable to check.", EditorType = ActionParameterEditorType.Choice)]
    public string VariableName { get; set; } = string.Empty;
    
    [ActionParameter("Operator", "Comparison operator (==, !=, >, <, >=, <=).")]
    public string Operator { get; set; } = "==";
    
    [ActionParameter("Value", "Value to compare against.")]
    public string CompareValue { get; set; } = string.Empty;
    
    [ActionParameter("Then Action", "Action to jump to if condition is TRUE.", EditorType = ActionParameterEditorType.ActionId)]
    public string ThenActionId { get; set; } = string.Empty;

    [ActionParameter("Else Action", "Action to jump to if condition is FALSE.", EditorType = ActionParameterEditorType.ActionId)]
    public string ElseActionId { get; set; } = string.Empty;
    
    // ===== Execute =====
    
    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ActionResult.Fail("Cancelled"));
        
        if (string.IsNullOrEmpty(VariableName))
            return Task.FromResult(ActionResult.Fail("Variable name not set"));
        
        bool conditionMet = EvaluateCondition(context);

if (conditionMet)
{
    if (!string.IsNullOrEmpty(ThenActionId))
        return Task.FromResult(ActionResult.Jump(ThenActionId));
    
    return Task.FromResult(ActionResult.Ok());  // condition true
}
else
{
    if (!string.IsNullOrEmpty(ElseActionId))
        return Task.FromResult(ActionResult.Jump(ElseActionId));
    
    return Task.FromResult(ActionResult.Fail("Condition not met"));  // ← condition false
}
        
        return Task.FromResult(ActionResult.Ok());
    }
    
    private bool EvaluateCondition(ScriptContext context)
    {
        // Try to get as different types
        var value = context.GetVariable<object>(VariableName, null!);
        
        if (value == null)
        {
            // Variable doesn't exist
            return Operator == "==" && CompareValue == "" 
                || Operator == "!=" && CompareValue != "";
        }
        
        // Try numeric comparison first
        if (double.TryParse(value.ToString(), out double numValue) &&
            double.TryParse(CompareValue, out double numCompare))
        {
            return Operator switch
            {
                "==" => Math.Abs(numValue - numCompare) < 0.0001,
                "!=" => Math.Abs(numValue - numCompare) >= 0.0001,
                ">" => numValue > numCompare,
                "<" => numValue < numCompare,
                ">=" => numValue >= numCompare,
                "<=" => numValue <= numCompare,
                _ => false
            };
        }
        
        // Boolean comparison
        if (value is bool boolValue)
        {
            bool boolCompare = CompareValue.ToLower() is "true" or "1" or "yes";
            return Operator switch
            {
                "==" => boolValue == boolCompare,
                "!=" => boolValue != boolCompare,
                _ => false
            };
        }
        
        // String comparison
        string strValue = value.ToString() ?? "";
        return Operator switch
        {
            "==" => strValue == CompareValue,
            "!=" => strValue != CompareValue,
            _ => false
        };
    }
}
