using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Executes a delegate function. Useful for code-based bots.
/// </summary>
public class DelegateAction : ActionBase
{
    private readonly Func<ScriptContext, CancellationToken, Task<ActionResult>> _action;

    public override string DisplayName => "Delegate Action";

    public DelegateAction(Func<ScriptContext, CancellationToken, Task<ActionResult>> action)
    {
        _action = action;
    }

    public DelegateAction(Action<ScriptContext> action)
    {
        _action = (ctx, ct) => 
        {
            action(ctx);
            return Task.FromResult(ActionResult.Ok());
        };
    }

    public override Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        return _action(context, ct);
    }
}
