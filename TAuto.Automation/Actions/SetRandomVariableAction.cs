using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Generates a random integer and stores it in a variable.
/// </summary>
public class SetRandomVariableAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"ðŸŽ² Set {TargetVariable} = Random({MinValue}, {MaxValue})";
    
    public string TargetVariable { get; set; } = "rnd";
    public int MinValue { get; set; } = 0;
    public int MaxValue { get; set; } = 100;

    private static readonly Random _random = new Random();

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(TargetVariable))
            return Task.FromResult(ActionResult.Fail("Target Variable name is empty"));

        int value = _random.Next(MinValue, MaxValue + 1); // Max is exclusive in Random.Next normally, so +1 to include it? 
        // Docs say: Next(min, max) -> max is Exclusive.
        // User typically expects "1 to 10" to include 10. So +1 is correct.
        
        context.SetVariable(TargetVariable, value);
        
        return Task.FromResult(ActionResult.Ok($"Set {TargetVariable} = {value}"));
    }
}
