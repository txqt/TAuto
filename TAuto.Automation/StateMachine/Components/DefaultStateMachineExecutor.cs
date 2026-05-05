using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public class DefaultStateMachineExecutor : IStateMachineExecutor
{
    private State? _currentState;
    private Dictionary<string, State>? _stateLookup;
    private StateTransition[]? _sortedGlobalTransitions;
    private StateTransition[]? _sortedStateTransitions;
    private readonly List<StateTransition> _activeGlobalsBuffer = new();

    public async Task<ActionResult> ExecuteAsync(StateMachine fsm, ScriptContext context, CancellationToken ct)
    {
        if (fsm.States.Count == 0) return ActionResult.Fail("State Machine has no states.");

        _stateLookup = fsm.States.ToDictionary(s => s.Name, s => s);
        _sortedGlobalTransitions = fsm.GlobalTransitions.OrderByDescending(t => t.Priority).ToArray();

        _currentState = FindState(fsm.InitialStateName);
        if (_currentState == null)
        {
            if (string.IsNullOrEmpty(fsm.InitialStateName)) _currentState = fsm.States.First();
            else return ActionResult.Fail($"Initial state '{fsm.InitialStateName}' not found.");
        }

        ITransitionEvaluator evaluator = new DefaultTransitionEvaluator(fsm.Trace);
        IVariableStore variableStore = new DefaultVariableStore(context);

        int transitionCount = 0;
        int consecutiveFallbackLoops = 0;
        
        while (!ct.IsCancellationRequested && _currentState != null)
        {
            var loopCheck = fsm.LoopMonitor.CheckTransitionCount(transitionCount++);
            if (loopCheck != null) return loopCheck;

            fsm.NotifyStateChanged(_currentState.Name);

            var stateStartTime = DateTime.UtcNow;

            fsm.LogTrace("StateEnter", _currentState.Name, elapsedMs: 0);

            if (fsm.EvaluateGlobalsBeforeEntry)
            {
                var globalWinner = await evaluator.EvaluateAsync(_sortedGlobalTransitions ?? Array.Empty<StateTransition>(), context, ct);
                if (globalWinner != null)
                {
                    var gtResult = await ExecuteTransitionAsync(fsm, globalWinner, _currentState, context, variableStore, stateStartTime, 0, ct);
                    if (gtResult != null) return gtResult;
                    continue; 
                }
            }

            if (ct.IsCancellationRequested) break;

            ActionResult entryResult = ActionResult.Ok("Default");
            try
            {
                entryResult = await fsm.ActionExecutor.ExecuteActionsAsync(_currentState.EntryActions, context, _currentState.Name, true, ct);
                fsm.LogTrace("EntryResult", _currentState.Name, details: $"success={entryResult.Success}, msg={entryResult.Message}");
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
                    variableStore.ClearLocalVariables(_currentState.Name);
                    var gtResult = await ExecuteTransitionAsync(fsm, errorTransition, _currentState, context, variableStore, stateStartTime, 0, ct);
                    if (gtResult != null) return gtResult;
                    await Task.Delay(10, ct);
                    continue;
                }
                else
                {
                    return entryResult;
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
            foreach (var gt in fsm.GlobalTransitions)
            {
                if (!transitionStartTimes.ContainsKey(gt)) { transitionStartTimes[gt] = DateTime.UtcNow; transitionRetryCounts[gt] = 0; }
            }

            while (!ct.IsCancellationRequested && !transitioned)
            {
                if (context.HealthMonitor != null)
                {
                    if (!context.HealthMonitor.CheckActivityTimeout() || context.HealthMonitor.Status != GameHealthStatus.Healthy)
                    {
                        var globalFallback = _sortedGlobalTransitions?.FirstOrDefault(t => t.Condition == null && t.IsFallback);
                        if (globalFallback != null && !string.Equals(_currentState.Name, globalFallback.ToState, StringComparison.OrdinalIgnoreCase))
                        {
                            var gtResult = await ExecuteTransitionAsync(fsm, globalFallback, _currentState, context, variableStore, stateStartTime, 0, ct);
                            if (gtResult != null) return gtResult;
                            transitioned = true;
                            context.HealthMonitor.Reset();
                            break;
                        }
                        else
                        {
                            return ActionResult.Fail($"Game unhealthy ({context.HealthMonitor.Status}), but no valid global fallback transition exists.");
                        }
                    }
                }

                int effectiveTimeout = _currentState.MaxDurationMs > 0 ? _currentState.MaxDurationMs : fsm.DefaultStateTimeoutMs;
                if (effectiveTimeout > 0 && (DateTime.UtcNow - stateStartTime).TotalMilliseconds >= effectiveTimeout)
                {
                    variableStore.ClearLocalVariables(_currentState.Name);
                    return ActionResult.Fail($"State '{_currentState.Name}' timed out after {effectiveTimeout}ms.");
                }

                await context.UpdateScreenCaptureAsync(force: false);

                _activeGlobalsBuffer.Clear();
                var now = DateTime.UtcNow;
                foreach (var gt in _sortedGlobalTransitions!)
                {
                    if (gt.TimeoutMs > 0 && (now - transitionStartTimes[gt]).TotalMilliseconds >= gt.TimeoutMs) continue;
                    if (gt.MaxRetries > 0 && transitionRetryCounts[gt] >= gt.MaxRetries) continue;
                    _activeGlobalsBuffer.Add(gt);
                }

                var pollGlobalWinner = await evaluator.EvaluateAsync(_activeGlobalsBuffer, context, ct);

                if (pollGlobalWinner != null)
                {
                    var gtResult = await ExecuteTransitionAsync(fsm, pollGlobalWinner, _currentState, context, variableStore, stateStartTime, pollCount, ct);
                    if (gtResult != null) return gtResult;
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
                        var result = await ExecuteTransitionAsync(fsm, transition, _currentState, context, variableStore, stateStartTime, pollCount, ct);
                        if (result != null) return result;
                        transitioned = true;
                        break;
                    }
                }

                if (!transitioned)
                {
                    bool allExhausted = _sortedStateTransitions!.Length == 0 || _sortedStateTransitions.All(t =>
                        (t.TimeoutMs > 0 && (DateTime.UtcNow - transitionStartTimes[t]).TotalMilliseconds >= t.TimeoutMs) ||
                        (t.MaxRetries > 0 && transitionRetryCounts[t] >= t.MaxRetries));
                    if (allExhausted && _currentState.MaxDurationMs == 0)
                    {
                        fsm.LogTrace("TransitionsExhausted", _currentState.Name, details: $"transitions={_sortedStateTransitions!.Length}, globals={_sortedGlobalTransitions?.Length ?? 0}, polls={pollCount}");
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

    private async Task<ActionResult?> ExecuteTransitionAsync(StateMachine fsm, StateTransition transition, State fromState, ScriptContext context, IVariableStore variableStore, DateTime stateStartTime, int pollCount, CancellationToken ct)
    {
        var transitionElapsedMs = (DateTime.UtcNow - stateStartTime).TotalMilliseconds;
        context.HealthMonitor?.ReportActivity();
        transition.LastFiredUtc = DateTime.UtcNow;

        foreach (var exitAction in fromState.ExitActions)
        {
            if (ct.IsCancellationRequested) break;
            try { await exitAction.ExecuteAsync(context, ct); }
            catch (Exception ex)
            {
                context.Logger?.Warning($"ExitAction failed in state '{fromState.Name}': {ex.Message}. Continuing transition.");
            }
        }
        
        variableStore.ClearLocalVariables(fromState.Name);

        if (!string.IsNullOrEmpty(context.JumpToId))
        {
            context.JumpToId = null;
        }

        if (string.Equals(transition.ToState, "END", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var action in transition.OnTransitionActions)
            {
                if (ct.IsCancellationRequested) break;
                try { await action.ExecuteAsync(context, ct); } catch { }
            }
            fsm.LogTrace("TransitionTrigger", fromState.Name, "END", details: "State machine completed", elapsedMs: transitionElapsedMs);
            return ActionResult.Ok("State Machine completed (reached END state).");
        }

        var nextState = FindState(transition.ToState);
        if (nextState == null) return ActionResult.Fail($"Target state '{transition.ToState}' not found.");

        foreach (var action in transition.OnTransitionActions)
        {
            if (ct.IsCancellationRequested) break;
            try { await action.ExecuteAsync(context, ct); } catch { }
        }

        fsm.LogTrace("TransitionTrigger", fromState.Name, nextState.Name, null, pollCount: pollCount, elapsedMs: transitionElapsedMs);
        fsm.Metrics.RecordStateTime(fromState.Name, transitionElapsedMs, pollCount);
        fsm.Metrics.RecordTransition(fromState.Name, nextState.Name);

        _currentState = nextState;
        return null;
    }

    private State? FindState(string? name) => !string.IsNullOrEmpty(name) && _stateLookup != null && _stateLookup.TryGetValue(name, out var state) ? state : null;

    private int CalculateOptimalWaitTime(State state, Dictionary<StateTransition, DateTime> transitionStartTimes, DateTime stateStartTime, int adaptiveInterval = 100)
    {
        if (state.Transitions.Count == 0 && (state.Transitions.All(t => t.TransitionType == TransitionType.Event || t.TransitionType == TransitionType.Immediate)))
        {
             // Simplified wait logic for the extracted component
             return adaptiveInterval;
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
}
