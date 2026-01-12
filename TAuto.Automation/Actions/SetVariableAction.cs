using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that sets or modifies a variable in ScriptContext.
/// Supports setting values and simple arithmetic operations.
/// </summary>
public class SetVariableAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => Operation switch
    {
        VariableOperation.Set => $"📝 Set {VariableName} = {Value}",
        VariableOperation.Add => $"📝 {VariableName} += {Value}",
        VariableOperation.Subtract => $"📝 {VariableName} -= {Value}",
        VariableOperation.Multiply => $"📝 {VariableName} *= {Value}",
        VariableOperation.Increment => $"📝 {VariableName}++",
        VariableOperation.Decrement => $"📝 {VariableName}--",
        _ => $"📝 {VariableName}"
    };
    
    // ===== Configuration =====
    
    /// <summary>
    /// Name of the variable to set/modify.
    /// </summary>
    public string VariableName { get; set; } = string.Empty;
    
    /// <summary>
    /// Value to set or use in operation.
    /// </summary>
    public string Value { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of operation to perform.
    /// </summary>
    public VariableOperation Operation { get; set; } = VariableOperation.Set;
    
    /// <summary>
    /// Value type hint for parsing.
    /// </summary>
    public VariableType ValueType { get; set; } = VariableType.Auto;
    
    // ===== Execute =====
    
    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return Task.FromResult(ActionResult.Fail("Cancelled"));
        
        if (string.IsNullOrEmpty(VariableName))
            return Task.FromResult(ActionResult.Fail("Variable name not set"));
        
        try
        {
            switch (Operation)
            {
                case VariableOperation.Set:
                    context.SetVariable(VariableName, ParseValue(Value));
                    break;
                    
                case VariableOperation.Increment:
                    context.Increment(VariableName, 1);
                    break;
                    
                case VariableOperation.Decrement:
                    context.Increment(VariableName, -1);
                    break;
                    
                case VariableOperation.Add:
                    var addValue = context.GetDouble(VariableName) + double.Parse(Value);
                    context.SetVariable(VariableName, addValue);
                    break;
                    
                case VariableOperation.Subtract:
                    var subValue = context.GetDouble(VariableName) - double.Parse(Value);
                    context.SetVariable(VariableName, subValue);
                    break;
                    
                case VariableOperation.Multiply:
                    var mulValue = context.GetDouble(VariableName) * double.Parse(Value);
                    context.SetVariable(VariableName, mulValue);
                    break;
            }
            
            return Task.FromResult(ActionResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(ActionResult.Fail($"Failed to set variable: {ex.Message}"));
        }
    }
    
    private object ParseValue(string value)
    {
        switch (ValueType)
        {
            case VariableType.Integer:
                return int.Parse(value);
            case VariableType.Double:
                return double.Parse(value);
            case VariableType.Boolean:
                return value.ToLower() is "true" or "1" or "yes";
            case VariableType.String:
                return value;
            case VariableType.Auto:
            default:
                // Try to auto-detect type
                if (int.TryParse(value, out int intVal))
                    return intVal;
                if (double.TryParse(value, out double doubleVal))
                    return doubleVal;
                if (value.ToLower() is "true" or "false")
                    return value.ToLower() == "true";
                return value;
        }
    }
}

public enum VariableOperation
{
    Set,
    Add,
    Subtract,
    Multiply,
    Increment,
    Decrement
}

public enum VariableType
{
    Auto,
    String,
    Integer,
    Double,
    Boolean
}
