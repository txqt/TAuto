using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using TAuto.Core;
using TAuto.Automation.StateMachine.Components;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// Controller for executing a state machine.
/// Refactored to compose components for execution, evaluation, and monitoring.
/// </summary>
public class StateMachine
{
    public StateMachine()
    {
        States = new List<State>();
        GlobalTransitions = new List<StateTransition>();
    }

    public List<State> States { get; set; }
    public string InitialStateName { get; set; } = string.Empty;
    public int DefaultStateTimeoutMs { get; set; } = 0;
    public event EventHandler<string>? OnStateChanged;

    [JsonIgnore]
    public IExecutionLoopMonitor LoopMonitor { get; set; } = new DefaultExecutionLoopMonitor();

    [JsonIgnore]
    public IActionExecutor ActionExecutor { get; set; } = new DefaultActionExecutor();

    [JsonIgnore]
    public IStateMachineExecutor Executor { get; set; } = new DefaultStateMachineExecutor();

    [JsonIgnore]
    public StateMachineTrace Trace { get; } = new();
    
    public event EventHandler<StateMachineTraceEntry>? OnTrace;

    [JsonIgnore]
    public StateMachineMetrics Metrics { get; } = new();

    public List<StateTransition> GlobalTransitions { get; set; }

    public bool EvaluateGlobalsBeforeEntry { get; set; } = true;

    public async Task<ActionResult> RunAsync(ScriptContext context, CancellationToken ct)
    {
        return await Executor.ExecuteAsync(this, context, ct);
    }

    internal void NotifyStateChanged(string stateName)
    {
        try { OnStateChanged?.Invoke(this, stateName); }
        catch (Exception ex) 
        { 
            // We don't have a logger in the FSM instance itself usually, 
            // but we can at least ensure this doesn't crash the loop.
            System.Diagnostics.Debug.WriteLine($"[StateMachine] OnStateChanged handler error: {ex.Message}"); 
        }
    }

    internal void LogTrace(string eventType, string stateName, string? toState = null, string? details = null, int pollCount = 0, double elapsedMs = 0)
    {
        Trace.Log(eventType, stateName, toState, details, pollCount, elapsedMs);
        var entry = new StateMachineTraceEntry
        {
            Timestamp = DateTime.UtcNow,
            EventType = eventType,
            StateName = stateName,
            TransitionTo = toState,
            Details = details,
            PollCount = pollCount,
            ElapsedMs = elapsedMs
        };

        StateMachineTraceRouter.Emit(entry);

        if (OnTrace != null && Trace.IsEnabled)
        {
            OnTrace.Invoke(this, entry);
        }
    }
}
