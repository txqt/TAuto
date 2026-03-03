using System;
using System.Collections.Generic;
using TAuto.Core;

namespace TAuto.Automation.StateMachine.Components;

public class DefaultExecutionLoopMonitor : IExecutionLoopMonitor
{
    public int MaxTransitionsPerWindow { get; set; } = 500;
    public int WindowMs { get; set; } = 5000; // 5 seconds
    public int MaxTransitions { get => MaxTransitionsPerWindow; set => MaxTransitionsPerWindow = value; }
    private readonly Queue<DateTime> _recentTransitions = new();

    public ActionResult? CheckTransitionCount(int _)
    {
        var now = DateTime.UtcNow;
        _recentTransitions.Enqueue(now);

        while (_recentTransitions.Count > 0 && (now - _recentTransitions.Peek()).TotalMilliseconds > WindowMs)
        {
            _recentTransitions.Dequeue();
        }

        if (_recentTransitions.Count > MaxTransitionsPerWindow)
        {
            return ActionResult.Fail($"Infinite loop detected: {_recentTransitions.Count} transitions in {WindowMs}ms.");
        }
        return null;
    }
}
