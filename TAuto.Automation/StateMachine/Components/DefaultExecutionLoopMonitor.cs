using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public class DefaultExecutionLoopMonitor : IExecutionLoopMonitor
{
    public int MaxTransitions { get; set; } = 1000;

    public ActionResult? CheckTransitionCount(int count)
    {
        if (count > MaxTransitions)
        {
            return ActionResult.Fail($"Max transitions ({MaxTransitions}) exceeded. Possible infinite loop.");
        }
        return null;
    }
}
