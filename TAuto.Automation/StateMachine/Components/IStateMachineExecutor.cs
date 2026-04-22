using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public interface IStateMachineExecutor
{
    Task<ActionResult> ExecuteAsync(StateMachine stateMachine, ScriptContext context, CancellationToken ct);
}
