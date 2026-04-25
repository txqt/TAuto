using TAuto.Core;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// A transition that triggers when a specific event is raised.
/// The event is consumed (removed) when the transition triggers.
/// </summary>
public class EventTransition : StateTransition
{
    public EventTransition()
    {
        TransitionType = TransitionType.Event;
    }

    /// <summary>
    /// The event name to listen for. If empty, behaves like a regular StateTransition.
    /// </summary>
    public string EventName { get; set; } = string.Empty;

    /// <summary>
    /// Evaluates if the transition should happen based on event + optional condition.
    /// </summary>
    public override async Task<bool> ShouldTransitionAsync(ScriptContext context, CancellationToken ct)
    {
        // If EventName is set, check for the event
        if (!string.IsNullOrEmpty(EventName))
        {
            // Try to consume the event - if it doesn't exist, don't transition
            if (!context.ConsumeEvent(EventName))
                return false;
        }

        // If there's also a Condition, check it too
        if (Condition != null)
        {
            try
            {
                var result = await Condition.ExecuteAsync(context, ct);
                return result.Success;
            }
            catch
            {
                return false;
            }
        }

        // Event was found (and consumed), no additional condition required
        return true;
    }
}
