using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public class DefaultActionExecutor : IActionExecutor
{
    public async Task<ActionResult> ExecuteActionsAsync(IEnumerable<IAction> actions, ScriptContext context, string stateName, bool ignoreJump, CancellationToken ct)
    {
        foreach (var action in actions)
        {
            if (ct.IsCancellationRequested) break;

            var actionResult = await action.ExecuteAsync(context, ct);
            if (!actionResult.Success && !action.ContinueOnError)
            {
                return ActionResult.Fail($"Action '{action.DisplayName}' failed in state '{stateName}': {actionResult.Message}");
            }
        }

        if (ignoreJump && !string.IsNullOrEmpty(context.JumpToId))
        {
            System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in actions of state '{stateName}'. Use Transitions instead.");
            context.JumpToId = null;
        }

        return ActionResult.Ok();
    }
}
