using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// Controller for executing a state machine.
/// </summary>
public class StateMachine
{
    public StateMachine()
    {
        States = new ObservableCollection<State>();
    }

    /// <summary>
    /// Collection of all states in this machine.
    /// </summary>
    public ObservableCollection<State> States { get; set; }

    /// <summary>
    /// The name of the state to start with.
    /// </summary>
    public string InitialStateName { get; set; } = string.Empty;

    /// <summary>
    /// Event fired when the state changes.
    /// </summary>
    public event EventHandler<string>? OnStateChanged;

    /// <summary>
    /// Max transitions to prevent infinite loops without user control.
    /// </summary>
    public int MaxTransitions { get; set; } = 1000;

    /// <summary>
    /// Trace log for debugging execution. Enable with Trace.IsEnabled = true.
    /// </summary>
    public StateMachineTrace Trace { get; } = new();

    /// <summary>
    /// Event fired when a trace entry is logged. Subscribe for real-time debugging.
    /// </summary>
    public event EventHandler<StateMachineTraceEntry>? OnTrace;

    /// <summary>
    /// Performance metrics for this state machine. Always recorded.
    /// </summary>
    public StateMachineMetrics Metrics { get; } = new();

    private State? _currentState;

    /// <summary>
    /// Execute the state machine logic with polling loop.
    /// State stays active until a transition condition is met or timeout occurs.
    /// </summary>
    public async Task<ActionResult> RunAsync(ScriptContext context, CancellationToken ct)
    {
        if (States.Count == 0)
        {
            return ActionResult.Fail("State Machine has no states.");
        }

        // Find initial state
        _currentState = States.FirstOrDefault(s => s.Name == InitialStateName);
        if (_currentState == null)
        {
            if (string.IsNullOrEmpty(InitialStateName))
            {
                // Default to first state if not set
                _currentState = States.First();
            }
            else
            {
                 return ActionResult.Fail($"Initial state '{InitialStateName}' not found.");
            }
        }

        int transitionCount = 0;
        
        while (!ct.IsCancellationRequested && _currentState != null)
        {
            if (transitionCount++ > MaxTransitions)
            {
                 return ActionResult.Fail($"Max transitions ({MaxTransitions}) exceeded. Possible infinite loop.");
            }

            OnStateChanged?.Invoke(this, _currentState.Name);
            var stateStartTime = DateTime.UtcNow;

            // 1. Execute Entry Actions ONCE
            var entryActions = _currentState.EntryActions.ToList();
            for (int i = 0; i < entryActions.Count; i++)
            {
                var action = entryActions[i];
                if (ct.IsCancellationRequested) break;
                
                var result = await action.ExecuteAsync(context, ct);
                if (!result.Success && !action.ContinueOnError)
                {
                    return ActionResult.Fail($"Action '{action.DisplayName}' failed in state '{_currentState.Name}': {result.Message}");
                }
            }
            
            // IGNORE Jump in Entry Actions (state logic should use Transitions)
            if (!string.IsNullOrEmpty(context.JumpToId))
            {
                System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in Entry actions of state '{_currentState.Name}'. Use Transitions instead.");
                context.JumpToId = null;
            }

            if (ct.IsCancellationRequested) break;

            // 2. HYBRID POLLING LOOP - wait for transition with event wake-up
            bool transitioned = false;
            var transitionStartTimes = new Dictionary<StateTransition, DateTime>();
            var transitionRetryCounts = new Dictionary<StateTransition, int>();
            int consecutiveFailures = 0; // For adaptive polling
            int pollCount = 0;
            
            // Initialize tracking for all transitions
            foreach (var t in _currentState.Transitions)
            {
                transitionStartTimes[t] = DateTime.UtcNow;
                transitionRetryCounts[t] = 0;
            }

            // Trace: State entered
            LogTrace("StateEnter", _currentState.Name, elapsedMs: 0);

            while (!ct.IsCancellationRequested && !transitioned)
            {
                // Check STATE-level timeout
                if (_currentState.MaxDurationMs > 0)
                {
                    var elapsed = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                    if (elapsed >= _currentState.MaxDurationMs)
                    {
                        return ActionResult.Fail($"State '{_currentState.Name}' timed out after {_currentState.MaxDurationMs}ms.");
                    }
                }

                // Check transitions (sorted by Priority DESC, fallbacks last)
                var sortedTransitions = _currentState.Transitions
                    .OrderBy(t => t.IsFallback) // Non-fallback first
                    .ThenByDescending(t => t.Priority)
                    .ToList();

                foreach (var transition in sortedTransitions)
                {
                    // Check TRANSITION-level timeout (skip if expired)
                    if (transition.TimeoutMs > 0)
                    {
                        var transitionElapsed = (DateTime.UtcNow - transitionStartTimes[transition]).TotalMilliseconds;
                        if (transitionElapsed >= transition.TimeoutMs)
                        {
                            continue; // Skip this transition, try next
                        }
                    }

                    // Check retry limit (skip if exceeded)
                    if (transition.MaxRetries > 0 && transitionRetryCounts[transition] >= transition.MaxRetries)
                    {
                        continue; // Skip this transition, try next
                    }

                    if (await transition.ShouldTransitionAsync(context, ct))
                    {
                        var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                        
                        // Trace: State exiting
                        LogTrace("StateExit", _currentState.Name, transition.ToState, pollCount: pollCount, elapsedMs: transitionElapsedMs);
                        
                        // Execute Exit Actions ONCE
                        foreach (var exitAction in _currentState.ExitActions)
                        {
                            if (ct.IsCancellationRequested) break;
                            await exitAction.ExecuteAsync(context, ct);
                        }
                        
                        // IGNORE Jump in Exit Actions
                        if (!string.IsNullOrEmpty(context.JumpToId))
                        {
                            System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in Exit actions of state '{_currentState.Name}'.");
                            context.JumpToId = null;
                        }

                        // Handle special "END" state - completes the state machine
                        if (string.Equals(transition.ToState, "END", StringComparison.OrdinalIgnoreCase))
                        {
                            // Execute OnTransitionActions even for END
                            foreach (var transitionAction in transition.OnTransitionActions)
                            {
                                if (ct.IsCancellationRequested) break;
                                await transitionAction.ExecuteAsync(context, ct);
                            }
                            
                            LogTrace("TransitionTrigger", _currentState.Name, "END", details: "State machine completed", elapsedMs: transitionElapsedMs);
                            return ActionResult.Ok("State Machine completed (reached END state).");
                        }

                        // Change state
                        var nextStateName = transition.ToState;
                        var nextState = States.FirstOrDefault(s => s.Name == nextStateName);
                        
                        if (nextState == null)
                        {
                            return ActionResult.Fail($"Target state '{nextStateName}' not found.");
                        }
                        
                        // Execute OnTransitionActions (during transition)
                        foreach (var transitionAction in transition.OnTransitionActions)
                        {
                            if (ct.IsCancellationRequested) break;
                            await transitionAction.ExecuteAsync(context, ct);
                        }
                        
                        // Trace: Transition triggered
                        LogTrace("TransitionTrigger", _currentState.Name, nextStateName, pollCount: pollCount, elapsedMs: transitionElapsedMs);
                        
                        // Record metrics
                        Metrics.RecordStateTime(_currentState.Name, transitionElapsedMs, pollCount);
                        Metrics.RecordTransition(_currentState.Name, nextStateName);
                        
                        _currentState = nextState;
                        transitioned = true;
                        break;
                    }
                    else
                    {
                        // Increment retry counter on failed check
                        transitionRetryCounts[transition]++;
                    }
                }

                // If no transition matched, use HYBRID WAIT with adaptive intervals
                if (!transitioned)
                {
                    consecutiveFailures++;
                    pollCount++;
                    
                    // Adaptive polling: switch to slow mode after threshold
                    int adaptiveInterval = consecutiveFailures >= _currentState.SlowdownThreshold
                        ? _currentState.SlowCheckIntervalMs
                        : _currentState.FastCheckIntervalMs;
                    
                    var waitTimeMs = CalculateOptimalWaitTime(_currentState, transitionStartTimes, stateStartTime, adaptiveInterval);
                    try
                    {
                        // Wait for event signal OR timeout (whichever comes first)
                        await context.EventSignal.WaitAsync(waitTimeMs, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // Graceful cancellation
                    }
                }
                else
                {
                    consecutiveFailures = 0; // Reset on successful transition
                }
            }
        }

        return ct.IsCancellationRequested ? ActionResult.Fail("Cancelled") : ActionResult.Ok("Completed");
    }

    /// <summary>
    /// Calculate optimal wait time based on transition types and timeouts.
    /// Returns short interval for polling transitions, or Timeout.Infinite for pure event-based states.
    /// </summary>
    private int CalculateOptimalWaitTime(
        State state, 
        Dictionary<StateTransition, DateTime> transitionStartTimes,
        DateTime stateStartTime,
        int adaptiveInterval = 100)
    {
        // Check if ALL active transitions are event-based (no polling needed)
        bool allEventBased = state.Transitions.All(t => 
            t.TransitionType == TransitionType.Event || 
            t.TransitionType == TransitionType.Immediate);
        
        if (allEventBased)
        {
            // Pure event-driven: wait indefinitely until event is raised
            // But still respect state timeout if set
            if (state.MaxDurationMs > 0)
            {
                var remaining = state.MaxDurationMs - (int)(DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                return Math.Max(10, remaining);
            }
            return Timeout.Infinite;
        }
        
        // Hybrid mode: use adaptive polling interval but respect transition timeouts
        int minWait = adaptiveInterval;
        
        foreach (var t in state.Transitions.Where(t => t.TimeoutMs > 0))
        {
            if (!transitionStartTimes.TryGetValue(t, out var startTime))
                continue;
                
            var remaining = t.TimeoutMs - (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            if (remaining > 0 && remaining < minWait)
            {
                minWait = remaining;
            }
        }
        
        // Also respect state timeout
        if (state.MaxDurationMs > 0)
        {
            var stateRemaining = state.MaxDurationMs - (int)(DateTime.UtcNow - stateStartTime).TotalMilliseconds;
            if (stateRemaining > 0 && stateRemaining < minWait)
            {
                minWait = stateRemaining;
            }
        }
        
        return Math.Max(10, minWait); // Minimum 10ms to prevent busy-loop
    }

    /// <summary>
    /// Log a trace entry and fire OnTrace event.
    /// </summary>
    private void LogTrace(string eventType, string stateName, string? toState = null, string? details = null, int pollCount = 0, double elapsedMs = 0)
    {
        Trace.Log(eventType, stateName, toState, details, pollCount, elapsedMs);
        
        if (OnTrace != null && Trace.IsEnabled)
        {
            var entry = new StateMachineTraceEntry
            {
                EventType = eventType,
                StateName = stateName,
                TransitionTo = toState,
                Details = details,
                PollCount = pollCount,
                ElapsedMs = elapsedMs
            };
            OnTrace.Invoke(this, entry);
        }
    }
}
