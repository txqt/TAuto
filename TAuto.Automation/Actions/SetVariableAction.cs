using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that sets or modifies a variable in ScriptContext.
/// </summary>
[ActionMetadata("Set Variable", "Logic", "V")]
public class SetVariableAction : ActionBase
{
    public override string DisplayName => Operation switch
    {
        VariableOperation.Set => $"Set {VariableName} = {Value}",
        VariableOperation.Add => $"{VariableName} += {Value}",
        VariableOperation.Subtract => $"{VariableName} -= {Value}",
        VariableOperation.Multiply => $"{VariableName} *= {Value}",
        VariableOperation.Increment => $"{VariableName}++",
        VariableOperation.Decrement => $"{VariableName}--",
        _ => VariableName
    };

    [ActionParameter("Variable Name", "Name of the variable to set or modify.")]
    public string VariableName { get; set; } = string.Empty;

    [ActionParameter("Value", "Value to store or use during the operation.")]
    public string Value { get; set; } = string.Empty;

    [ActionParameter("Operation", "Operation to apply to the variable.")]
    public VariableOperation Operation { get; set; } = VariableOperation.Set;

    [ActionParameter("Value Type", "Hint for parsing the input value.", IsAdvanced = true)]
    public VariableType ValueType { get; set; } = VariableType.Auto;

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromResult(ActionResult.Fail("Cancelled"));
        }

        if (string.IsNullOrEmpty(VariableName))
        {
            return Task.FromResult(ActionResult.Fail("Variable name not set"));
        }

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
                    context.SetVariable(VariableName, context.GetDouble(VariableName) + double.Parse(Value));
                    break;
                case VariableOperation.Subtract:
                    context.SetVariable(VariableName, context.GetDouble(VariableName) - double.Parse(Value));
                    break;
                case VariableOperation.Multiply:
                    context.SetVariable(VariableName, context.GetDouble(VariableName) * double.Parse(Value));
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
        return ValueType switch
        {
            VariableType.Integer => int.Parse(value),
            VariableType.Double => double.Parse(value),
            VariableType.Boolean => value.ToLowerInvariant() is "true" or "1" or "yes",
            VariableType.String => value,
            _ => AutoParse(value)
        };
    }

    private static object AutoParse(string value)
    {
        if (int.TryParse(value, out var intValue))
        {
            return intValue;
        }

        if (double.TryParse(value, out var doubleValue))
        {
            return doubleValue;
        }

        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return value;
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
