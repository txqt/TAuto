namespace TAuto.Automation.StateMachine;

/// <summary>
/// Classifies how a transition is evaluated by the state machine runtime.
/// </summary>
public enum TransitionType
{
    /// <summary>
    /// Requires polling (vision, OCR, screen checks). Default behavior.
    /// </summary>
    Polling,
    
    /// <summary>
    /// Event-driven (responds to RaiseEvent immediately via signal).
    /// </summary>
    Event,
    
    /// <summary>
    /// Variable-based (can be checked instantly).
    /// </summary>
    Variable,
    
    /// <summary>
    /// Always true (unconditional transition, checked immediately).
    /// </summary>
    Immediate
}
