using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.StateMachine;
using TAuto.Core;

namespace TAuto.Automation.BehaviorTree;

/// <summary>
/// Result status of a BT node tick.
/// </summary>
public enum NodeStatus
{
    /// <summary>Node completed successfully.</summary>
    Success,
    /// <summary>Node failed.</summary>
    Failure,
    /// <summary>Node is still executing (long-running, e.g. an FSM).</summary>
    Running
}

/// <summary>
/// Base class for all Behavior Tree nodes.
/// </summary>
public abstract class BtNode
{
    public string Name { get; set; } = "";

    /// <summary>
    /// Execute one "tick" of this node.
    /// </summary>
    public abstract Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct);

    /// <summary>
    /// Called when this node (or a parent) is interrupted by a higher-priority sibling.
    /// Override to perform cleanup (e.g. cancel a running FSM).
    /// </summary>
    public virtual Task ResetAsync()
    {
        return Task.CompletedTask;
    }
}

// ════════════════════════════════════════════════════════════
// Composite Nodes
// ════════════════════════════════════════════════════════════

/// <summary>
/// Selector (Priority/Fallback): tries children in order.
/// Returns Success/Running on the FIRST child that succeeds or is running.
/// Returns Failure only if ALL children fail.
/// 
/// KEY FEATURE: If a higher-priority child succeeds while a lower-priority
/// child is Running, the lower-priority child is interrupted (Reset).
/// </summary>
public class SelectorNode : BtNode
{
    public List<BtNode> Children { get; } = new();

    /// <summary>
    /// Index of the currently Running child (-1 if none).
    /// </summary>
    private int _runningChildIndex = -1;

    public override async Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (ct.IsCancellationRequested) return NodeStatus.Failure;

            var status = await Children[i].TickAsync(context, ct);

            if (status == NodeStatus.Success || status == NodeStatus.Running)
            {
                // If a HIGHER priority child won while a LOWER priority child was running,
                // interrupt the lower-priority child.
                if (_runningChildIndex >= 0 && _runningChildIndex != i)
                {
                    await Children[_runningChildIndex].ResetAsync();
                }

                _runningChildIndex = status == NodeStatus.Running ? i : -1;
                return status;
            }
            // Failure → try next child
        }

        _runningChildIndex = -1;
        return NodeStatus.Failure;
    }

    public override async Task ResetAsync()
    {
        if (_runningChildIndex >= 0 && _runningChildIndex < Children.Count)
        {
            await Children[_runningChildIndex].ResetAsync();
        }
        _runningChildIndex = -1;
    }
}

/// <summary>
/// Sequence: runs children in order.
/// Returns Failure on the FIRST child that fails.
/// Returns Success only if ALL children succeed.
/// Returns Running if a child is running (resumes from that child on next tick).
/// </summary>
public class SequenceNode : BtNode
{
    public List<BtNode> Children { get; } = new();

    private int _currentIndex = 0;

    public override async Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct)
    {
        for (; _currentIndex < Children.Count; _currentIndex++)
        {
            if (ct.IsCancellationRequested) return NodeStatus.Failure;

            var status = await Children[_currentIndex].TickAsync(context, ct);

            if (status == NodeStatus.Failure)
            {
                _currentIndex = 0; // Reset for next tick
                return NodeStatus.Failure;
            }

            if (status == NodeStatus.Running)
            {
                return NodeStatus.Running;
            }
            // Success → continue to next child
        }

        _currentIndex = 0; // Reset for next tick
        return NodeStatus.Success;
    }

    public override async Task ResetAsync()
    {
        if (_currentIndex < Children.Count)
        {
            await Children[_currentIndex].ResetAsync();
        }
        _currentIndex = 0;
    }
}

// ════════════════════════════════════════════════════════════
// Leaf Nodes
// ════════════════════════════════════════════════════════════

/// <summary>
/// Condition: wraps an IAction as a boolean check.
/// Returns Success if the action succeeds, Failure otherwise.
/// Never returns Running (conditions are instant checks).
/// </summary>
public class ConditionNode : BtNode
{
    public IAction? Condition { get; set; }

    /// <summary>
    /// If true, inverts the result (NOT logic).
    /// </summary>
    public bool Invert { get; set; } = false;

    public override async Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct)
    {
        if (Condition == null) return NodeStatus.Success;

        try
        {
            var result = await Condition.ExecuteAsync(context, ct);
            bool success = result.Success;
            if (Invert) success = !success;
            return success ? NodeStatus.Success : NodeStatus.Failure;
        }
        catch (OperationCanceledException)
        {
            return NodeStatus.Failure;
        }
        catch
        {
            return Invert ? NodeStatus.Success : NodeStatus.Failure;
        }
    }
}

/// <summary>
/// ActionLeaf: wraps an IAction and executes it.
/// Returns Success if the action succeeds, Failure otherwise.
/// </summary>
public class ActionLeafNode : BtNode
{
    public IAction? Action { get; set; }

    public override async Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct)
    {
        if (Action == null) return NodeStatus.Failure;

        try
        {
            var result = await Action.ExecuteAsync(context, ct);
            return result.Success ? NodeStatus.Success : NodeStatus.Failure;
        }
        catch (OperationCanceledException)
        {
            return NodeStatus.Failure;
        }
        catch
        {
            return NodeStatus.Failure;
        }
    }
}

/// <summary>
/// FsmNode: wraps a StateMachine and runs it as a long-running task.
/// Returns Running while the FSM is executing.
/// Returns Success when the FSM completes successfully.
/// Returns Failure if the FSM fails.
/// 
/// On Reset (interrupt): cancels the FSM via CancellationToken.
/// </summary>
public class FsmNode : BtNode
{
    /// <summary>
    /// Factory function that builds a fresh StateMachine instance.
    /// Called each time the FSM needs to start (allows re-execution).
    /// </summary>
    public Func<StateMachine.StateMachine>? FsmFactory { get; set; }

    /// <summary>
    /// Max time (ms) to wait for FSM to respond to cancellation. Default 3000ms.
    /// </summary>
    public int InterruptTimeoutMs { get; set; } = 3000;

    private Task<ActionResult>? _runningTask;
    private CancellationTokenSource? _fsmCts;
    private bool _completed;
    private ActionResult? _lastResult;

    public override async Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct)
    {
        // If already completed from a previous tick, return the result
        if (_completed)
        {
            var result = _lastResult;
            Reset(); // Clean up for potential re-entry
            return result != null && result.Success ? NodeStatus.Success : NodeStatus.Failure;
        }

        // If not yet started, start the FSM
        if (_runningTask == null)
        {
            if (FsmFactory == null) return NodeStatus.Failure;

            var fsm = FsmFactory();
            _fsmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var fsmToken = _fsmCts.Token;

            _runningTask = Task.Run(async () => await fsm.RunAsync(context, fsmToken), fsmToken);
        }

        // Check if the task has completed
        if (_runningTask.IsCompleted)
        {
            _completed = true;
            try
            {
                _lastResult = await _runningTask;
            }
            catch (OperationCanceledException)
            {
                _lastResult = ActionResult.Fail("FSM cancelled");
            }
            catch (Exception ex)
            {
                _lastResult = ActionResult.Fail($"FSM error: {ex.Message}");
            }

            return _lastResult.Success ? NodeStatus.Success : NodeStatus.Failure;
        }

        // Still running
        return NodeStatus.Running;
    }

    public override async Task ResetAsync()
    {
        if (_fsmCts != null && !_fsmCts.IsCancellationRequested)
        {
            _fsmCts.Cancel();

            // Wait for FSM to respond to cancellation (with timeout)
            if (_runningTask != null && !_runningTask.IsCompleted)
            {
                var completed = await Task.WhenAny(
                    _runningTask,
                    Task.Delay(InterruptTimeoutMs));

                if (completed != _runningTask)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[BT] FsmNode '{Name}': FSM did not respond to cancellation within {InterruptTimeoutMs}ms");
                }
            }
        }

        Reset();
    }

    private void Reset()
    {
        _fsmCts?.Dispose();
        _fsmCts = null;
        _runningTask = null;
        _completed = false;
        _lastResult = null;
    }
}

/// <summary>
/// InlineConditionNode: wraps a delegate as a condition check.
/// Useful for quick inline checks without creating an IAction.
/// </summary>
public class InlineConditionNode : BtNode
{
    private readonly Func<ScriptContext, CancellationToken, Task<bool>> _check;

    public InlineConditionNode(string name, Func<ScriptContext, CancellationToken, Task<bool>> check)
    {
        Name = name;
        _check = check;
    }

    public override async Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct)
    {
        try
        {
            var result = await _check(context, ct);
            return result ? NodeStatus.Success : NodeStatus.Failure;
        }
        catch
        {
            return NodeStatus.Failure;
        }
    }
}

/// <summary>
/// InlineActionNode: wraps a delegate as an action.
/// </summary>
public class InlineActionNode : BtNode
{
    private readonly Func<ScriptContext, CancellationToken, Task<NodeStatus>> _action;

    public InlineActionNode(string name, Func<ScriptContext, CancellationToken, Task<NodeStatus>> action)
    {
        Name = name;
        _action = action;
    }

    public override async Task<NodeStatus> TickAsync(ScriptContext context, CancellationToken ct)
    {
        try
        {
            return await _action(context, ct);
        }
        catch
        {
            return NodeStatus.Failure;
        }
    }
}
