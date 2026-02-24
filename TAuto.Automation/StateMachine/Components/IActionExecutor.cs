using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public interface IActionExecutor
{
    Task<ActionResult> ExecuteActionsAsync(IEnumerable<IAction> actions, ScriptContext context, string stateName, bool ignoreJump, CancellationToken ct);
}
