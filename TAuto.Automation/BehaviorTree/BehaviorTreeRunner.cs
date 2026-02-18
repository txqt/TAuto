using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.BehaviorTree;

/// <summary>
/// Executes a Behavior Tree by ticking the root node at a configurable interval.
/// 
/// The runner is the "heartbeat" of the BT. Each tick:
/// 1. Evaluates the tree from the root (highest-priority decisions first)
/// 2. If a node returns Running (e.g. an FSM is executing), waits before next tick
/// 3. If a higher-priority node wins while a lower one is Running, the lower one is interrupted
/// 
/// This replaces the need for GlobalTransitions and RunActionsWithMonitorAsync.
/// </summary>
public class BehaviorTreeRunner
{
    /// <summary>
    /// The root node of the behavior tree.
    /// </summary>
    public BtNode Root { get; set; }

    /// <summary>
    /// Interval (ms) between ticks when a node is Running. Default 2000ms.
    /// Lower = faster interrupt response, higher CPU usage.
    /// For game bots, 1000-3000ms is typically sufficient.
    /// </summary>
    public int TickIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Interval (ms) between ticks when the tree returns Success/Failure (idle).
    /// Default 5000ms. The tree completed a full cycle and needs to restart.
    /// </summary>
    public int IdleIntervalMs { get; set; } = 5000;

    /// <summary>
    /// Maximum number of ticks before the runner stops. 0 = unlimited.
    /// Safety valve to prevent infinite loops during development.
    /// </summary>
    public int MaxTicks { get; set; } = 0;

    /// <summary>
    /// Event fired on each tick with the result status. Useful for logging/UI.
    /// </summary>
    public event Action<int, NodeStatus, string>? OnTick;

    /// <summary>
    /// Event fired when the runner starts/stops.
    /// </summary>
    public event Action<bool>? OnRunningStateChanged;

    public BehaviorTreeRunner(BtNode root)
    {
        Root = root;
    }

    /// <summary>
    /// Run the behavior tree tick loop until cancellation or max ticks.
    /// Returns ActionResult indicating how the runner stopped.
    /// </summary>
    public async Task<ActionResult> RunAsync(ScriptContext context, CancellationToken ct)
    {
        if (Root == null)
        {
            return ActionResult.Fail("BehaviorTreeRunner: No root node set.");
        }

        OnRunningStateChanged?.Invoke(true);
        int tickCount = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                tickCount++;

                if (MaxTicks > 0 && tickCount > MaxTicks)
                {
                    return ActionResult.Ok($"BT completed: max ticks ({MaxTicks}) reached.");
                }

                var sw = Stopwatch.StartNew();
                NodeStatus status;

                try
                {
                    status = await Root.TickAsync(context, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BT Runner] Tick {tickCount} error: {ex.Message}");
                    status = NodeStatus.Failure;
                }

                sw.Stop();
                OnTick?.Invoke(tickCount, status, $"{sw.ElapsedMilliseconds}ms");

                // Determine wait time based on status
                int waitMs = status == NodeStatus.Running
                    ? TickIntervalMs    // FSM is running, check back soon
                    : IdleIntervalMs;   // Tree cycle completed, wait longer

                try
                {
                    await Task.Delay(waitMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            // Clean up any running children
            try
            {
                await Root.ResetAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BT Runner] Cleanup error: {ex.Message}");
            }

            OnRunningStateChanged?.Invoke(false);
        }

        return ct.IsCancellationRequested
            ? ActionResult.Fail("BT cancelled.")
            : ActionResult.Ok($"BT completed after {tickCount} ticks.");
    }
}
