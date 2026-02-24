using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public class DefaultTransitionEvaluator : ITransitionEvaluator
{
    private readonly StateMachineTrace _trace;

    public DefaultTransitionEvaluator(StateMachineTrace trace)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    public async Task<StateTransition?> EvaluateAsync(IEnumerable<StateTransition> transitions, ScriptContext context, CancellationToken ct)
    {
        foreach (var gt in transitions)
        {
            if (ct.IsCancellationRequested) return null;

            try
            {
                if (await gt.ShouldTransitionAsync(context, ct))
                {
                    return gt;
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                _trace.Log("TransitionCheckError", "?", gt.ToState, $"Transition check failed: {ex.Message}");
            }
        }
        return null;
    }
}
