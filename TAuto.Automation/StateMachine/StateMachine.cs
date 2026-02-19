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
/// Evaluates Global Transitions sequentially before each state's entry actions and in the polling loop.
/// No parallel tasks, no atomic gates — simple and deterministic.
/// </summary>
public class StateMachine
{
    public StateMachine()
    {
        States = new ObservableCollection<State>();
        GlobalTransitions = new ObservableCollection<StateTransition>();
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

    /// <summary>
    /// Global transitions checked in EVERY state before state-specific transitions.
    /// Use for high-priority interrupts (e.g., "Under Attack", "Disconnected").
    /// Evaluated sequentially before entry actions and in the polling loop.
    /// </summary>
    public ObservableCollection<StateTransition> GlobalTransitions { get; set; }

    private State? _currentState;

    /// <summary>
    /// O(1) state lookup. Built once at RunAsync start (immutable during execution).
    /// </summary>
    private Dictionary<string, State>? _stateLookup;

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

        // Build immutable state lookup (O(1) access during execution)
        _stateLookup = States.ToDictionary(s => s.Name, s => s);

        // Find initial state
        _currentState = FindState(InitialStateName);
        if (_currentState == null)
        {
            if (string.IsNullOrEmpty(InitialStateName))
            {
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

            // Trace: State entered
            LogTrace("StateEnter", _currentState.Name, elapsedMs: 0);

            // ────────────────────────────────────────────────────
            // 1. Check Global Transitions BEFORE entry actions
            // ────────────────────────────────────────────────────
            var globalWinner = await CheckGlobalTransitionsAsync(context, ct);
            if (globalWinner != null)
            {
                var result = await ExecuteTransitionAsync(globalWinner, _currentState, context, stateStartTime, 0, ct);
                if (result != null) return result;
                continue; // State changed, restart loop
            }

            if (ct.IsCancellationRequested) break;

            // ────────────────────────────────────────────────────
            // 2. Execute Entry Actions sequentially
            // ────────────────────────────────────────────────────
            foreach (var action in _currentState.EntryActions)
            {
                if (ct.IsCancellationRequested) break;

                var actionResult = await action.ExecuteAsync(context, ct);
                if (!actionResult.Success && !action.ContinueOnError)
                {
                    return ActionResult.Fail($"Action '{action.DisplayName}' failed in state '{_currentState.Name}': {actionResult.Message}");
                }
            }

            // IGNORE Jump in Entry Actions
            if (!string.IsNullOrEmpty(context.JumpToId))
            {
                System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in Entry actions of state '{_currentState.Name}'. Use Transitions instead.");
                context.JumpToId = null;
            }

            if (ct.IsCancellationRequested) break;

            // ────────────────────────────────────────────────────
            // 3. POLLING LOOP - check globals first, then state transitions
            // ────────────────────────────────────────────────────
            bool transitioned = false;
            var transitionStartTimes = new Dictionary<StateTransition, DateTime>();
            var transitionRetryCounts = new Dictionary<StateTransition, int>();
            int consecutiveFailures = 0;
            int pollCount = 0;
            
            // Initialize tracking for all transitions (state + global)
            foreach (var t in _currentState.Transitions)
            {
                transitionStartTimes[t] = DateTime.UtcNow;
                transitionRetryCounts[t] = 0;
            }
            foreach (var gt in GlobalTransitions)
            {
                if (!transitionStartTimes.ContainsKey(gt))
                {
                    transitionStartTimes[gt] = DateTime.UtcNow;
                    transitionRetryCounts[gt] = 0;
                }
            }

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

                // ── GLOBAL TRANSITIONS (highest priority interrupts) ──
                foreach (var globalTransition in GlobalTransitions.OrderByDescending(t => t.Priority))
                {
                    if (ct.IsCancellationRequested) break;
                    
                    if (await globalTransition.ShouldTransitionAsync(context, ct))
                    {
                        var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                        
                        LogTrace("GlobalInterrupt", _currentState.Name, globalTransition.ToState,
                            details: "Global transition triggered", pollCount: pollCount, elapsedMs: transitionElapsedMs);

                        var result = await ExecuteTransitionAsync(globalTransition, _currentState, context, stateStartTime, pollCount, ct);
                        if (result != null) return result;
                        transitioned = true;
                        break;
                    }
                }
                
                if (transitioned) continue;

                // ── STATE-SPECIFIC TRANSITIONS (sorted by Priority DESC, fallbacks last) ──
                var sortedTransitions = _currentState.Transitions
                    .OrderBy(t => t.IsFallback)
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
                            continue;
                        }
                    }

                    // Check retry limit (skip if exceeded)
                    if (transition.MaxRetries > 0 && transitionRetryCounts[transition] >= transition.MaxRetries)
                    {
                        continue;
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
                        
                        // Clear scoped local variables for the exiting state
                        context.ClearLocalVariables(_currentState.Name);
                        
                        // IGNORE Jump in Exit Actions
                        if (!string.IsNullOrEmpty(context.JumpToId))
                        {
                            System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in Exit actions of state '{_currentState.Name}'.");
                            context.JumpToId = null;
                        }

                        // Handle special "END" state
                        if (string.Equals(transition.ToState, "END", StringComparison.OrdinalIgnoreCase))
                        {
                            foreach (var transitionAction in transition.OnTransitionActions)
                            {
                                if (ct.IsCancellationRequested) break;
                                await transitionAction.ExecuteAsync(context, ct);
                            }
                            
                            LogTrace("TransitionTrigger", _currentState.Name, "END", details: "State machine completed", elapsedMs: transitionElapsedMs);
                            return ActionResult.Ok("State Machine completed (reached END state).");
                        }

                        // Change state
                        var nextState = FindState(transition.ToState);
                        
                        if (nextState == null)
                        {
                            return ActionResult.Fail($"Target state '{transition.ToState}' not found.");
                        }
                        
                        // Execute OnTransitionActions
                        foreach (var transitionAction in transition.OnTransitionActions)
                        {
                            if (ct.IsCancellationRequested) break;
                            await transitionAction.ExecuteAsync(context, ct);
                        }
                        
                        LogTrace("TransitionTrigger", _currentState.Name, nextState.Name, pollCount: pollCount, elapsedMs: transitionElapsedMs);
                        
                        Metrics.RecordStateTime(_currentState.Name, transitionElapsedMs, pollCount);
                        Metrics.RecordTransition(_currentState.Name, nextState.Name);
                        
                        _currentState = nextState;
                        transitioned = true;
                        break;
                    }
                    else
                    {
                        transitionRetryCounts[transition]++;
                    }
                }

                // If no transition matched, use adaptive wait
                if (!transitioned)
                {
                    consecutiveFailures++;
                    pollCount++;
                    
                    // Urgent mode: force fast polling for sub-100ms reaction (combat, alerts)
                    int adaptiveInterval;
                    if (context.IsUrgentMode)
                    {
                        adaptiveInterval = Math.Min(20, _currentState.FastCheckIntervalMs);
                    }
                    else
                    {
                        adaptiveInterval = consecutiveFailures >= _currentState.SlowdownThreshold
                            ? _currentState.SlowCheckIntervalMs
                            : _currentState.FastCheckIntervalMs;
                    }
                    
                    var waitTimeMs = CalculateOptimalWaitTime(_currentState, transitionStartTimes, stateStartTime, adaptiveInterval);
                    try
                    {
                        await context.EventSignal.WaitAsync(waitTimeMs, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                else
                {
                    consecutiveFailures = 0;
                }
            }
        }

        return ct.IsCancellationRequested ? ActionResult.Fail("Cancelled") : ActionResult.Ok("Completed");
    }

    // ════════════════════════════════════════════════════════════
    // Sequential Global Transition Check
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Check all global transitions sequentially. Returns the first matching transition, or null.
    /// </summary>
    private async Task<StateTransition?> CheckGlobalTransitionsAsync(ScriptContext context, CancellationToken ct)
    {
        foreach (var gt in GlobalTransitions.OrderByDescending(t => t.Priority))
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
                LogTrace("GlobalCheckError", _currentState?.Name ?? "?", gt.ToState,
                    details: $"Global transition check failed: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// Execute a transition: run exit actions, transition actions, and change state.
    /// Returns ActionResult if the machine should stop (END or error), null to continue.
    /// </summary>
    private async Task<ActionResult?> ExecuteTransitionAsync(
        StateTransition transition,
        State fromState,
        ScriptContext context,
        DateTime stateStartTime,
        int pollCount,
        CancellationToken ct)
    {
        var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;

        // Execute Exit Actions
        foreach (var exitAction in fromState.ExitActions)
        {
            if (ct.IsCancellationRequested) break;
            await exitAction.ExecuteAsync(context, ct);
        }

        // Clear scoped local variables
        context.ClearLocalVariables(fromState.Name);

        // Handle END state
        if (string.Equals(transition.ToState, "END", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var transitionAction in transition.OnTransitionActions)
            {
                if (ct.IsCancellationRequested) break;
                await transitionAction.ExecuteAsync(context, ct);
            }
            LogTrace("TransitionTrigger", fromState.Name, "END", details: "State machine completed", elapsedMs: transitionElapsedMs);
            return ActionResult.Ok("State Machine completed (reached END state).");
        }

        // Find next state
        var nextState = FindState(transition.ToState);
        if (nextState == null)
        {
            return ActionResult.Fail($"Target state '{transition.ToState}' not found.");
        }

        // Execute OnTransitionActions
        foreach (var transitionAction in transition.OnTransitionActions)
        {
            if (ct.IsCancellationRequested) break;
            await transitionAction.ExecuteAsync(context, ct);
        }

        LogTrace("TransitionTrigger", fromState.Name, nextState.Name,
            details: "Global interrupt transition", pollCount: pollCount, elapsedMs: transitionElapsedMs);

        Metrics.RecordStateTime(fromState.Name, transitionElapsedMs, pollCount);
        Metrics.RecordTransition(fromState.Name, nextState.Name);

        _currentState = nextState;
        return null; // Continue loop
    }

    // ════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// O(1) state lookup using pre-built dictionary.
    /// </summary>
    private State? FindState(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return _stateLookup != null && _stateLookup.TryGetValue(name, out var state) ? state : null;
    }

    /// <summary>
    /// Calculate optimal wait time based on transition types and timeouts.
    /// </summary>
    private int CalculateOptimalWaitTime(
        State state, 
        Dictionary<StateTransition, DateTime> transitionStartTimes,
        DateTime stateStartTime,
        int adaptiveInterval = 100)
    {
        bool allEventBased = state.Transitions.All(t => 
            t.TransitionType == TransitionType.Event || 
            t.TransitionType == TransitionType.Immediate);
        
        if (allEventBased)
        {
            if (state.MaxDurationMs > 0)
            {
                var remaining = state.MaxDurationMs - (int)(DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                return Math.Max(10, remaining);
            }
            return Timeout.Infinite;
        }
        
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
        
        if (state.MaxDurationMs > 0)
        {
            var stateRemaining = state.MaxDurationMs - (int)(DateTime.UtcNow - stateStartTime).TotalMilliseconds;
            if (stateRemaining > 0 && stateRemaining < minWait)
            {
                minWait = stateRemaining;
            }
        }
        
        return Math.Max(10, minWait);
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
