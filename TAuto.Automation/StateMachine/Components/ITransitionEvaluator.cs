using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public interface ITransitionEvaluator
{
    Task<StateTransition?> EvaluateAsync(IEnumerable<StateTransition> transitions, ScriptContext context, CancellationToken ct);
}
