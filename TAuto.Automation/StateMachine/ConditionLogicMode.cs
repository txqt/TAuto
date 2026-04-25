namespace TAuto.Automation.StateMachine;

/// <summary>
/// Logic mode for combining multiple conditions in a transition.
/// </summary>
public enum ConditionLogicMode
{
    /// <summary>
    /// Single condition (default). Only Condition property is used.
    /// </summary>
    Single,
    
    /// <summary>
    /// ALL conditions must pass (AND logic).
    /// </summary>
    And,
    
    /// <summary>
    /// ANY condition must pass (OR logic).
    /// </summary>
    Or,
    
    /// <summary>
    /// Invert the result of the condition(s).
    /// </summary>
    Not
}
