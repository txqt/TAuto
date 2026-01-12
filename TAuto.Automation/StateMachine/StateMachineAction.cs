using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// An Action that executes a State Machine.
/// </summary>
public class StateMachineAction : IAction
{
    public StateMachineAction()
    {
        Machine = new StateMachine();
    }

    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    public string DisplayName => $"State Machine: {Machine.InitialStateName}";
    
    public bool IsBreakpoint { get; set; }
    
    public int RetryCount { get; set; }
    
    public int RetryIntervalMs { get; set; } = 1000;
    
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// The State Machine instance containing all logic.
    /// </summary>
    public StateMachine Machine { get; set; }

    public async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (Machine == null) return ActionResult.Fail("No State Machine defined.");
        
        // Delegate execution to the machine
        return await Machine.RunAsync(context, ct);
    }
}
