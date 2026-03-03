using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;
using TAuto.Automation.StateMachine.Components;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// Controller for executing a state machine.
/// Refactored to compose components (IActionExecutor, ITransitionEvaluator, IExecutionLoopMonitor, IVariableStore).
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

    public IExecutionLoopMonitor LoopMonitor { get; set; } = new DefaultExecutionLoopMonitor();
    public IActionExecutor ActionExecutor { get; set; } = new DefaultActionExecutor();

    public StateMachineTrace Trace { get; } = new();
    public event EventHandler<StateMachineTraceEntry>? OnTrace;
    public StateMachineMetrics Metrics { get; } = new();

    public List<StateTransition> GlobalTransitions { get; set; }

    /// <summary>
    /// AUDIT FIX (CORRECTION-4): When true (default), global transitions are evaluated
    /// BEFORE entry actions on each state entry. Set to false if global transitions
    /// cause entry-action starvation (globals always match → entry actions never run).
    /// </summary>
    public bool EvaluateGlobalsBeforeEntry { get; set; } = true;

    private State? _currentState;
    private Dictionary<string, State>? _stateLookup;
    private StateTransition[]? _sortedGlobalTransitions;
    private StateTransition[]? _sortedStateTransitions;

    public async Task<ActionResult> RunAsync(ScriptContext context, CancellationToken ct)
    {
        if (States.Count == 0) return ActionResult.Fail("State Machine has no states.");

        // ISSUE-12: Clear stale instance fields for safe re-entrant calls
        _currentState = null;
        _sortedStateTransitions = null;

        _stateLookup = States.ToDictionary(s => s.Name, s => s);
        _sortedGlobalTransitions = GlobalTransitions.OrderByDescending(t => t.Priority).ToArray();

        _currentState = FindState(InitialStateName);
        if (_currentState == null)
        {
            if (string.IsNullOrEmpty(InitialStateName)) _currentState = States.First();
            else return ActionResult.Fail($"Initial state '{InitialStateName}' not found.");
        }

        ITransitionEvaluator evaluator = new DefaultTransitionEvaluator(Trace);
        IVariableStore variableStore = new DefaultVariableStore(context);

        int transitionCount = 0;
        int consecutiveFallbackLoops = 0;
        
        while (!ct.IsCancellationRequested && _currentState != null)
        {
            var loopCheck = LoopMonitor.CheckTransitionCount(transitionCount++);
            if (loopCheck != null) return loopCheck;

            // AUDIT FIX (CORRECTION-6): Protect FSM from subscriber exceptions
            try { OnStateChanged?.Invoke(this, _currentState.Name); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[StateMachine] OnStateChanged handler error: {ex.Message}"); }

            var stateStartTime = DateTime.UtcNow;

            LogTrace("StateEnter", _currentState.Name, elapsedMs: 0);

            // AUDIT FIX (CORRECTION-4): Pre-entry global evaluation is configurable.
            // When enabled (default), globals can interrupt before entry actions run.
            // Disable if globals always match → entry actions are never executed.
            if (EvaluateGlobalsBeforeEntry)
            {
                var globalWinner = await evaluator.EvaluateAsync(_sortedGlobalTransitions, context, ct);
                if (globalWinner != null)
                {
                    var gtResult = await ExecuteTransitionAsync(globalWinner, _currentState, context, variableStore, stateStartTime, 0, ct);
                    if (gtResult != null) return gtResult;
                    continue; 
                }
            }

            if (ct.IsCancellationRequested) break;

            ActionResult entryResult = ActionResult.Ok("Default");
            try
            {
                entryResult = await ActionExecutor.ExecuteActionsAsync(_currentState.EntryActions, context, _currentState.Name, true, ct);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StateMachine] Entry actions error in state '{_currentState.Name}': {ex.Message}");
                entryResult = ActionResult.Fail($"Entry exception: {ex.Message}");
            }
            
            if (entryResult.Success)
            {
                consecutiveFallbackLoops = 0;
            }
            else
            {
                consecutiveFallbackLoops++;
                if (consecutiveFallbackLoops > 3)
                {
                    return ActionResult.Fail($"State Machine stuck in fallback loop in state '{_currentState.Name}' after 3 consecutive entry failures.");
                }

                _sortedStateTransitions = _currentState.Transitions.OrderBy(t => t.IsFallback).ThenByDescending(t => t.Priority).ToArray();
                var errorTransition = _sortedStateTransitions.FirstOrDefault(t => t.Condition == null && t.IsFallback);
                if (errorTransition == null)
                {
                    errorTransition = _sortedGlobalTransitions?.FirstOrDefault(t => t.Condition == null && t.IsFallback);
                }

                if (errorTransition != null)
                {
                    // FIX-2 (Audit): Clear local variables from the failed state before fallback
                    // to prevent stale data from poisoning the recovery state.
                    variableStore.ClearLocalVariables(_currentState.Name);
                    System.Diagnostics.Debug.WriteLine($"[StateMachine] Fallback transition triggered from '{_currentState.Name}' to '{errorTransition.ToState}' due to entry failure.");
                    var gtResult = await ExecuteTransitionAsync(errorTransition, _currentState, context, variableStore, stateStartTime, 0, ct);
                    if (gtResult != null) return gtResult;

                    // AUDIT FIX-1: Add delay to prevent infinite hot CPU spin
                    // if fallback states repeatedly fail their entry actions.
                    await Task.Delay(10, ct);

                    continue; // Skip the rest, move immediately to fallback state
                }
                else
                {
                    return entryResult; // No fallback state found, abort.
                }
            }

            if (ct.IsCancellationRequested) break;

            _sortedStateTransitions = _currentState.Transitions.OrderBy(t => t.IsFallback).ThenByDescending(t => t.Priority).ToArray();
            
            bool transitioned = false;
            var transitionStartTimes = new Dictionary<StateTransition, DateTime>();
            var transitionRetryCounts = new Dictionary<StateTransition, int>();
            int consecutiveFailures = 0;
            int pollCount = 0;
            
            foreach (var t in _currentState.Transitions) { transitionStartTimes[t] = DateTime.UtcNow; transitionRetryCounts[t] = 0; }
            foreach (var gt in GlobalTransitions)
            {
                if (!transitionStartTimes.ContainsKey(gt)) { transitionStartTimes[gt] = DateTime.UtcNow; transitionRetryCounts[gt] = 0; }
            }

            while (!ct.IsCancellationRequested && !transitioned)
            {
                int effectiveTimeout = _currentState.MaxDurationMs > 0 ? _currentState.MaxDurationMs : DefaultStateTimeoutMs;
                if (effectiveTimeout > 0 && (DateTime.UtcNow - stateStartTime).TotalMilliseconds >= effectiveTimeout)
                {
                    variableStore.ClearLocalVariables(_currentState.Name);
                    return ActionResult.Fail($"State '{_currentState.Name}' timed out after {effectiveTimeout}ms.");
                }

                // Audit FIX-2: Check TimeoutMs and MaxRetries for global transitions inside the loop
                var activeGlobals = _sortedGlobalTransitions!
                    .Where(gt => 
                        (gt.TimeoutMs == 0 || (DateTime.UtcNow - transitionStartTimes[gt]).TotalMilliseconds < gt.TimeoutMs) &&
                        (gt.MaxRetries == 0 || transitionRetryCounts[gt] < gt.MaxRetries))
                    .ToArray();

                var pollGlobalWinner = await evaluator.EvaluateAsync(activeGlobals, context, ct);

                if (pollGlobalWinner != null)
                {
                    var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                    LogTrace("GlobalInterrupt", _currentState.Name, pollGlobalWinner.ToState, "Global transition triggered", pollCount, transitionElapsedMs);
                    var gResult = await ExecuteTransitionAsync(pollGlobalWinner, _currentState, context, variableStore, stateStartTime, pollCount, ct);
                    if (gResult != null) return gResult;
                    transitioned = true;
                    break;
                }

                if (transitioned) continue;

                foreach (var transition in _sortedStateTransitions!)
                {
                    if (transition.TimeoutMs > 0 && (DateTime.UtcNow - transitionStartTimes[transition]).TotalMilliseconds >= transition.TimeoutMs) continue;
                    if (transition.MaxRetries > 0 && transitionRetryCounts[transition] >= transition.MaxRetries) continue;

                    bool shouldTransition = false;
                    try
                    {
                        shouldTransition = await transition.ShouldTransitionAsync(context, ct);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[StateMachine] Transition evaluation error: {ex.Message}");
                        transitionRetryCounts[transition]++;
                        continue;
                    }

                    if (shouldTransition)
                    {
                        var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                        LogTrace("StateExit", _currentState.Name, transition.ToState, null, pollCount: pollCount, elapsedMs: transitionElapsedMs);

                        var result = await ExecuteTransitionAsync(transition, _currentState, context, variableStore, stateStartTime, pollCount, ct);
                        if (result != null) return result;
                        
                        // FIX-5 (Reverted): Do NOT reset `transitionCount = 0;` here.
                        // Resetting it on every transition prevents LoopMonitor.MaxTransitions
                        // from ever catching infinite loops between states, causing 100% CPU lockups.
                        
                        transitioned = true;
                        break;
                    }
                }

                if (!transitioned)
                {
                    // ISSUE-6: Detect when all transitions are permanently exhausted
                    bool allExhausted = _sortedStateTransitions!.Length == 0 || _sortedStateTransitions.All(t =>
                        (t.TimeoutMs > 0 && (DateTime.UtcNow - transitionStartTimes[t]).TotalMilliseconds >= t.TimeoutMs) ||
                        (t.MaxRetries > 0 && transitionRetryCounts[t] >= t.MaxRetries));
                    if (allExhausted && _currentState.MaxDurationMs == 0)
                    {
                        variableStore.ClearLocalVariables(_currentState.Name);
                        return ActionResult.Fail($"State '{_currentState.Name}': all transitions exhausted with no state timeout set.");
                    }

                    consecutiveFailures++;
                    pollCount++;
                    
                    int adaptiveInterval = context.IsUrgentMode ? Math.Min(20, _currentState.FastCheckIntervalMs) : (consecutiveFailures >= _currentState.SlowdownThreshold ? _currentState.SlowCheckIntervalMs : _currentState.FastCheckIntervalMs);
                    
                    var waitTimeMs = CalculateOptimalWaitTime(_currentState, transitionStartTimes, stateStartTime, adaptiveInterval);
                    try { await context.EventSignal.WaitAsync(waitTimeMs, ct); } catch (OperationCanceledException) { break; }
                }
                else
                {
                    consecutiveFailures = 0;
                }
            }
        }

        return ct.IsCancellationRequested ? ActionResult.Fail("Cancelled") : ActionResult.Ok("Completed");
    }

    private async Task<ActionResult?> ExecuteTransitionAsync(StateTransition transition, State fromState, ScriptContext context, IVariableStore variableStore, DateTime stateStartTime, int pollCount, CancellationToken ct)
    {
        var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;

        // Record transition fire time for cooldown tracking
        transition.LastFiredUtc = DateTime.UtcNow;

        // FIX-2 (Audit): Exit actions are best-effort teardowns.
        // A failing exit action must NOT crash the state machine or bypass fallback mechanics.
        foreach (var exitAction in fromState.ExitActions)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await exitAction.ExecuteAsync(context, ct);
            }
            catch (Exception ex)
            {
                context.Logger?.Warning($"ExitAction failed in state '{fromState.Name}': {ex.Message}. Continuing transition.");
                System.Diagnostics.Debug.WriteLine($"[StateMachine] WARNING: ExitAction failed in state '{fromState.Name}': {ex.Message}. Continuing transition.");
            }
        }
        
        variableStore.ClearLocalVariables(fromState.Name);

        if (!string.IsNullOrEmpty(context.JumpToId))
        {
            System.Diagnostics.Debug.WriteLine($"[StateMachine] Warning: Jump ignored in Exit actions of state '{fromState.Name}'. Use Transitions instead.");
            context.JumpToId = null;
        }

        if (string.Equals(transition.ToState, "END", StringComparison.OrdinalIgnoreCase))
        {
            // ISSUE-5: OnTransitionActions on END path are best-effort (same as exit actions)
            foreach (var action in transition.OnTransitionActions)
            {
                if (ct.IsCancellationRequested) break;
                try { await action.ExecuteAsync(context, ct); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[StateMachine] WARNING: OnTransitionAction failed on END: {ex.Message}");
                }
            }
            LogTrace("TransitionTrigger", fromState.Name, "END", details: "State machine completed", elapsedMs: transitionElapsedMs);
            return ActionResult.Ok("State Machine completed (reached END state).");
        }

        var nextState = FindState(transition.ToState);
        if (nextState == null) return ActionResult.Fail($"Target state '{transition.ToState}' not found.");

        foreach (var action in transition.OnTransitionActions)
        {
            if (ct.IsCancellationRequested) break;
            try { await action.ExecuteAsync(context, ct); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StateMachine] WARNING: OnTransitionAction failed transitioning to '{nextState.Name}': {ex.Message}. Continuing transition.");
            }
        }

        LogTrace("TransitionTrigger", fromState.Name, nextState.Name, null, pollCount: pollCount, elapsedMs: transitionElapsedMs);
        Metrics.RecordStateTime(fromState.Name, transitionElapsedMs, pollCount);
        Metrics.RecordTransition(fromState.Name, nextState.Name);

        _currentState = nextState;
        return null;
    }

    private State? FindState(string? name) => !string.IsNullOrEmpty(name) && _stateLookup != null && _stateLookup.TryGetValue(name, out var state) ? state : null;

    private int CalculateOptimalWaitTime(State state, Dictionary<StateTransition, DateTime> transitionStartTimes, DateTime stateStartTime, int adaptiveInterval = 100)
    {
        bool hasNoTransitions = state.Transitions.Count == 0;
        bool allEventBased = !hasNoTransitions && state.Transitions.All(t => t.TransitionType == TransitionType.Event || t.TransitionType == TransitionType.Immediate);
        
        if (hasNoTransitions && (!EvaluateGlobalsBeforeEntry || GlobalTransitions.Count > 0))
        {
            return adaptiveInterval;
        }

        if (allEventBased)
        {
            if (state.MaxDurationMs > 0)
            {
                var remaining = state.MaxDurationMs - (int)(DateTime.UtcNow - stateStartTime).TotalMilliseconds;
                return Math.Max(10, remaining);
            }
            return 60000; // Cap at 60s
        }
        
        int minWait = adaptiveInterval;
        foreach (var t in state.Transitions.Where(t => t.TimeoutMs > 0))
        {
            if (!transitionStartTimes.TryGetValue(t, out var startTime)) continue;
            var remaining = t.TimeoutMs - (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            if (remaining > 0 && remaining < minWait) minWait = remaining;
        }
        
        if (state.MaxDurationMs > 0)
        {
            var stateRemaining = state.MaxDurationMs - (int)(DateTime.UtcNow - stateStartTime).TotalMilliseconds;
            if (stateRemaining > 0 && stateRemaining < minWait) minWait = stateRemaining;
        }
        return Math.Max(10, minWait);
    }

    private void LogTrace(string eventType, string stateName, string? toState = null, string? details = null, int pollCount = 0, double elapsedMs = 0)
    {
        Trace.Log(eventType, stateName, toState, details, pollCount, elapsedMs);
        
        if (OnTrace != null && Trace.IsEnabled)
        {
            OnTrace.Invoke(this, new StateMachineTraceEntry { EventType = eventType, StateName = stateName, TransitionTo = toState, Details = details, PollCount = pollCount, ElapsedMs = elapsedMs });
        }
    }
}
