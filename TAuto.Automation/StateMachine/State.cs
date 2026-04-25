using System.Collections.Generic;
using System.Text.Json.Serialization;
using TAuto.Core;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// Represents a single state in the state machine.
/// </summary>
public class State
{
    public State()
    {
        EntryActions = new List<IAction>();
        ExitActions = new List<IAction>();
        Transitions = new List<StateTransition>();
    }

    /// <summary>
    /// Unique name of the state.
    /// </summary>
    public string Name { get; set; } = "New State";

    /// <summary>
    /// Actions to execute when entering this state.
    /// </summary>
    public List<IAction> EntryActions { get; set; }

    /// <summary>
    /// Actions to execute when exiting this state.
    /// </summary>
    public List<IAction> ExitActions { get; set; }

    /// <summary>
    /// Possible transitions from this state. Checked in order.
    /// </summary>
    public List<StateTransition> Transitions { get; set; }
    
    /// <summary>
    /// Maximum time (ms) to stay in this state before failing. 0 = no limit.
    /// </summary>
    public int MaxDurationMs { get; set; } = 0;
    
    /// <summary>
    /// Polling interval (ms) between transition checks. Default 100ms.
    /// </summary>
    public int CheckIntervalMs { get; set; } = 100;
    
    /// <summary>
    /// Fast polling interval (ms) for urgent checks after recent activity. Default 50ms.
    /// </summary>
    public int FastCheckIntervalMs { get; set; } = 50;
    
    /// <summary>
    /// Slow polling interval (ms) for idle scanning when nothing is happening. Default 500ms.
    /// </summary>
    public int SlowCheckIntervalMs { get; set; } = 500;
    
    /// <summary>
    /// Number of consecutive failed transition checks before switching to slow mode. Default 3.
    /// </summary>
    public int SlowdownThreshold { get; set; } = 3;

}
