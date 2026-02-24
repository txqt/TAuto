using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public interface IExecutionLoopMonitor
{
    int MaxTransitions { get; set; }
    ActionResult? CheckTransitionCount(int count);
}
