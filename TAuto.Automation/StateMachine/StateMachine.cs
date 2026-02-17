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
/// Supports concurrent Global Transition monitoring with atomic transition gates.
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
    /// These are evaluated first in the polling loop and can override any state.
    /// </summary>
    public ObservableCollection<StateTransition> GlobalTransitions { get; set; }

    /// <summary>
    /// Interval (ms) for the concurrent Global Transition monitor. Default 100ms.
    /// Lower = faster interrupt response, higher CPU. Configurable per environment.
    /// </summary>
    public int MonitorIntervalMs { get; set; } = 100;

    /// <summary>
    /// Max time (ms) to wait for an action to respond to cancellation before forcing transition.
    /// Default 3000ms. If an action doesn't respect CancellationToken, this prevents hanging.
    /// </summary>
    public int InterruptTimeoutMs { get; set; } = 3000;

    private State? _currentState;
    
    /// <summary>
    /// Atomic transition gate. 0 = idle, 1 = transition in progress.
    /// Prevents double-transition when Monitor and Polling race.
    /// </summary>
    private int _transitioning = 0;

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

            // Reset gate for new state
            Interlocked.Exchange(ref _transitioning, 0);

            OnStateChanged?.Invoke(this, _currentState.Name);
            var stateStartTime = DateTime.UtcNow;

            // Create a linked CTS for this state visit (allows cancellation of actions)
            using var stateCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var stateToken = stateCts.Token;

            // Trace: State entered
            LogTrace("StateEnter", _currentState.Name, elapsedMs: 0);

            // ────────────────────────────────────────────────────
            // 1. Execute Entry Actions with Concurrent Global Monitor
            // ────────────────────────────────────────────────────
            StateTransition? globalWinner = null;

            if (_currentState.IsInterruptible && GlobalTransitions.Count > 0)
            {
                // Run actions + monitor concurrently
                globalWinner = await RunActionsWithMonitorAsync(
                    _currentState.EntryActions.ToList(),
                    _currentState,
                    context,
                    stateCts,
                    stateToken,
                    ct);
            }
            else
            {
                // Non-interruptible or no global transitions: run actions sequentially
                var entryResult = await RunActionsSequentialAsync(
                    _currentState.EntryActions.ToList(),
                    _currentState.Name,
                    context,
                    stateToken);
                
                if (entryResult != null) return entryResult;
            }

            // IGNORE Jump in Entry Actions
            if (!string.IsNullOrEmpty(context.JumpToId))
            {
                System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in Entry actions of state '{_currentState.Name}'. Use Transitions instead.");
                context.JumpToId = null;
            }

            // If global monitor won during entry actions, handle the transition
            if (globalWinner != null)
            {
                var interruptResult = await HandleGlobalInterruptAsync(
                    globalWinner, _currentState, context, stateStartTime, 0, ct);
                
                if (interruptResult.Completed)
                {
                    if (interruptResult.Result != null) return interruptResult.Result;
                    // State changed, continue loop
                    continue;
                }
            }

            if (ct.IsCancellationRequested) break;

            // ────────────────────────────────────────────────────
            // 2. HYBRID POLLING LOOP - wait for transition with concurrent global monitor
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
                        // Atomic gate: ensure only one transition wins
                        if (Interlocked.CompareExchange(ref _transitioning, 1, 0) != 0)
                        {
                            LogTrace("GateBlocked", _currentState.Name, globalTransition.ToState,
                                details: "Global transition blocked by atomic gate (another transition in progress)");
                            continue;
                        }

                        var interruptResult = await HandleGlobalInterruptAsync(
                            globalTransition, _currentState, context, stateStartTime, pollCount, ct);
                        
                        if (interruptResult.Completed)
                        {
                            if (interruptResult.Result != null) return interruptResult.Result;
                            transitioned = true;
                            break;
                        }
                        
                        // If not completed, reset gate
                        Interlocked.Exchange(ref _transitioning, 0);
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
                        // Atomic gate: ensure only one transition wins
                        if (Interlocked.CompareExchange(ref _transitioning, 1, 0) != 0)
                        {
                            LogTrace("GateBlocked", _currentState.Name, transition.ToState,
                                details: "Local transition blocked by atomic gate");
                            continue;
                        }

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

                // If no transition matched, use HYBRID WAIT with adaptive intervals
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
    // Concurrent Entry Actions + Global Monitor
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Run entry actions concurrently with a global transition monitor.
    /// Returns the winning global transition if interrupted, null otherwise.
    /// </summary>
    private async Task<StateTransition?> RunActionsWithMonitorAsync(
        List<IAction> actions,
        State state,
        ScriptContext context,
        CancellationTokenSource stateCts,
        CancellationToken stateToken,
        CancellationToken outerCt)
    {
        StateTransition? winner = null;

        // Action task: execute entry actions sequentially
        var actionTask = Task.Run(async () =>
        {
            foreach (var action in actions)
            {
                if (stateToken.IsCancellationRequested) break;

                var result = await action.ExecuteAsync(context, stateToken);
                if (!result.Success && !action.ContinueOnError)
                {
                    throw new ActionFailedException(
                        $"Action '{action.DisplayName}' failed in state '{state.Name}': {result.Message}");
                }
            }
        }, stateToken);

        // Monitor task: check global transitions concurrently
        var monitorTask = Task.Run(async () =>
        {
            // Add small jitter to avoid stampeding with other monitors
            var jitter = new Random().Next(0, MonitorIntervalMs / 4);
            await Task.Delay(jitter, stateToken);

            while (!stateToken.IsCancellationRequested)
            {
                foreach (var gt in GlobalTransitions.OrderByDescending(t => t.Priority))
                {
                    if (stateToken.IsCancellationRequested) break;

                    try
                    {
                        if (await gt.ShouldTransitionAsync(context, stateToken))
                        {
                            // Atomic gate
                            if (Interlocked.CompareExchange(ref _transitioning, 1, 0) == 0)
                            {
                                return gt; // Winner
                            }
                            
                            LogTrace("GateBlocked", state.Name, gt.ToState,
                                details: "Monitor gate blocked during entry actions");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        return null;
                    }
                    catch (Exception ex)
                    {
                        LogTrace("MonitorError", state.Name, gt.ToState,
                            details: $"Monitor check failed: {ex.Message}");
                    }
                }

                try
                {
                    await Task.Delay(MonitorIntervalMs, stateToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            return null;
        }, stateToken);

        try
        {
            var completedTask = await Task.WhenAny(actionTask, monitorTask);

            if (completedTask == monitorTask && !monitorTask.IsFaulted && !monitorTask.IsCanceled)
            {
                winner = await monitorTask;
                if (winner != null)
                {
                    LogTrace("GlobalInterrupt", state.Name, winner.ToState,
                        details: "Global transition triggered during entry actions — cancelling current action");

                    // Cancel actions
                    stateCts.Cancel();

                    // Wait for action task to respond to cancellation (with timeout)
                    var actionCompleted = await Task.WhenAny(actionTask, Task.Delay(InterruptTimeoutMs, outerCt));
                    if (actionCompleted != actionTask)
                    {
                        LogTrace("InterruptTimeout", state.Name, winner.ToState,
                            details: $"Action did not respond to cancellation within {InterruptTimeoutMs}ms — forcing transition");
                    }

                    // Run interruption cleanup actions
                    foreach (var cleanupAction in state.InterruptionActions)
                    {
                        if (outerCt.IsCancellationRequested) break;
                        try
                        {
                            await cleanupAction.ExecuteAsync(context, outerCt);
                        }
                        catch (Exception ex)
                        {
                            LogTrace("CleanupError", state.Name, details: $"Interruption cleanup failed: {ex.Message}");
                        }
                    }

                    return winner;
                }
            }

            // Actions completed first — cancel monitor
            if (!monitorTask.IsCompleted)
            {
                stateCts.Cancel();
                try { await monitorTask; } catch { /* expected cancellation */ }
            }

            // Check if action task faulted
            if (actionTask.IsFaulted)
            {
                var innerEx = actionTask.Exception?.InnerException;
                if (innerEx is ActionFailedException afe)
                {
                    return null; // Caller will handle via sequential path
                }
                throw innerEx ?? actionTask.Exception!;
            }
        }
        catch (OperationCanceledException) when (outerCt.IsCancellationRequested)
        {
            // Outer cancellation — propagate
            throw;
        }
        catch (ActionFailedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogTrace("ConcurrencyError", state.Name, details: $"Concurrent execution error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Run actions sequentially without concurrent monitoring.
    /// Returns ActionResult if a fatal error occurs, null on success.
    /// </summary>
    private async Task<ActionResult?> RunActionsSequentialAsync(
        List<IAction> actions,
        string stateName,
        ScriptContext context,
        CancellationToken ct)
    {
        foreach (var action in actions)
        {
            if (ct.IsCancellationRequested) break;

            var result = await action.ExecuteAsync(context, ct);
            if (!result.Success && !action.ContinueOnError)
            {
                return ActionResult.Fail($"Action '{action.DisplayName}' failed in state '{stateName}': {result.Message}");
            }
        }
        return null;
    }

    // ════════════════════════════════════════════════════════════
    // Global Interrupt Handler
    // ════════════════════════════════════════════════════════════

    private struct InterruptResult
    {
        public bool Completed;
        public ActionResult? Result;
    }

    /// <summary>
    /// Handle a global interrupt: exit current state, run transition actions, change state.
    /// </summary>
    private async Task<InterruptResult> HandleGlobalInterruptAsync(
        StateTransition globalTransition,
        State fromState,
        ScriptContext context,
        DateTime stateStartTime,
        int pollCount,
        CancellationToken ct)
    {
        var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;

        LogTrace("GlobalInterrupt", fromState.Name, globalTransition.ToState,
            details: "Global transition triggered", pollCount: pollCount, elapsedMs: transitionElapsedMs);

        // Execute Exit Actions of current state
        foreach (var exitAction in fromState.ExitActions)
        {
            if (ct.IsCancellationRequested) break;
            await exitAction.ExecuteAsync(context, ct);
        }

        // Clear scoped local variables for the exiting state
        context.ClearLocalVariables(fromState.Name);

        // Handle END state
        if (string.Equals(globalTransition.ToState, "END", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var transitionAction in globalTransition.OnTransitionActions)
            {
                if (ct.IsCancellationRequested) break;
                await transitionAction.ExecuteAsync(context, ct);
            }
            LogTrace("TransitionTrigger", fromState.Name, "END", details: "Global interrupt → END", elapsedMs: transitionElapsedMs);
            return new InterruptResult { Completed = true, Result = ActionResult.Ok("State Machine completed (global transition → END).") };
        }

        // Change state
        var nextState = FindState(globalTransition.ToState);
        if (nextState == null)
        {
            return new InterruptResult
            {
                Completed = true,
                Result = ActionResult.Fail($"Global transition target state '{globalTransition.ToState}' not found.")
            };
        }

        foreach (var transitionAction in globalTransition.OnTransitionActions)
        {
            if (ct.IsCancellationRequested) break;
            await transitionAction.ExecuteAsync(context, ct);
        }

        LogTrace("TransitionTrigger", fromState.Name, nextState.Name,
            details: "Global interrupt transition", pollCount: pollCount, elapsedMs: transitionElapsedMs);

        Metrics.RecordStateTime(fromState.Name, transitionElapsedMs, pollCount);
        Metrics.RecordTransition(fromState.Name, nextState.Name);

        _currentState = nextState;
        return new InterruptResult { Completed = true, Result = null };
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

/// <summary>
/// Internal exception for action failures during concurrent execution.
/// </summary>
internal class ActionFailedException : Exception
{
    public ActionFailedException(string message) : base(message) { }
}
