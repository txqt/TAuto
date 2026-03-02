using System.Text.Json.Serialization;
using TAuto.Core;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// Defines a transition rule between states.
/// </summary>
public class StateTransition
{
    /// <summary>
    /// The name of the target state to transition to.
    /// </summary>
    public string ToState { get; set; } = string.Empty;
    
    /// <summary>
    /// Priority of this transition. Higher priority = checked first. Default 0.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// The condition action that determines if this transition should occur.
    /// If the action executes successfully (Result.Success == true), the transition triggers.
    /// typically an "If" action or a logic check.
    /// If null, it's an unconditional transition (always triggers).
    /// </summary>
    public IAction? Condition { get; set; }

    /// <summary>
    /// Multiple conditions for composite logic (AND/OR). Used when LogicMode != Single.
    /// </summary>
    public List<IAction> Conditions { get; set; } = new();

    /// <summary>
    /// Logic mode for combining conditions. Default: Single (uses Condition property only).
    /// </summary>
    public ConditionLogicMode LogicMode { get; set; } = ConditionLogicMode.Single;

    /// <summary>
    /// Timeout (ms) for this specific transition. 0 = no timeout (uses state timeout).
    /// When expired, this transition is skipped for the rest of the state's duration.
    /// </summary>
    public int TimeoutMs { get; set; } = 0;

    /// <summary>
    /// Max retry count for this transition. 0 = unlimited retries.
    /// After exceeding, transition is disabled until state re-entry.
    /// </summary>
    public int MaxRetries { get; set; } = 0;

    /// <summary>
    /// Transition type hint for the state machine runtime.
    /// Used to optimize polling behavior (event-based vs polling-based).
    /// </summary>
    public TransitionType TransitionType { get; set; } = TransitionType.Polling;

    /// <summary>
    /// Actions to execute DURING transition (after exit actions, before entering next state).
    /// Useful for logging, analytics, state-specific cleanup.
    /// </summary>
    public List<IAction> OnTransitionActions { get; set; } = new();

    /// <summary>
    /// If true, this transition is the fallback (checked last, after all others fail).
    /// Equivalent to Priority = int.MinValue but more explicit.
    /// </summary>
    public bool IsFallback { get; set; } = false;

    /// <summary>
    /// Probability (0.0–1.0) that this transition fires when its condition matches.
    /// 1.0 = always fire (deterministic, default). 0.7 = 70% chance, 30% falls through.
    /// </summary>
    public double Probability { get; set; } = 1.0;

    /// <summary>
    /// If Probability roll fails, transition to this state instead (optional).
    /// Null = skip this transition and check the next one ("hesitation").
    /// </summary>
    public string? AlternativeTargetState { get; set; }

    /// <summary>
    /// Cooldown (ms) after this transition fires. Prevents rapid-fire repetition.
    /// 0 = no cooldown.
    /// </summary>
    public int CooldownMs { get; set; } = 0;

    /// <summary>Timestamp of last successful fire (transient, not serialized).</summary>
    [JsonIgnore]
    public DateTime LastFiredUtc { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Evaluates if the transition should happen based on the condition(s).
    /// </summary>
    public virtual async Task<bool> ShouldTransitionAsync(ScriptContext context, CancellationToken ct)
    {
        // Cooldown check
        if (CooldownMs > 0 && (DateTime.UtcNow - LastFiredUtc).TotalMilliseconds < CooldownMs)
            return false;

        bool result = await EvaluateConditionsAsync(context, ct);
        
        // Apply NOT logic if specified
        if (LogicMode == ConditionLogicMode.Not)
        {
            result = !result;
        }

        // Probability roll (only when condition matched)
        if (result && Probability < 1.0)
        {
            if (Random.Shared.NextDouble() >= Probability)
            {
                // Roll failed — this transition does NOT fire.
                // AlternativeTargetState is handled by the caller (StateMachine.cs).
                return false;
            }
        }
        
        return result;
    }

    /// <summary>
    /// Evaluate conditions based on LogicMode.
    /// </summary>
    private async Task<bool> EvaluateConditionsAsync(ScriptContext context, CancellationToken ct)
    {
        // Single mode: use Condition property
        if (LogicMode == ConditionLogicMode.Single || LogicMode == ConditionLogicMode.Not)
        {
            if (Condition == null) return true;
            
            try
            {
                var result = await Condition.ExecuteAsync(context, ct);
                return result.Success;
            }
            catch (OperationCanceledException)
            {
                return false; // Expected during cancellation
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[StateTransition] Condition failed → Target='{ToState}', Priority={Priority}, Error={ex.Message}");
                return false;
            }
        }

        // AND/OR mode: use Conditions list
        if (Conditions.Count == 0) return true; // No conditions = always true

        if (LogicMode == ConditionLogicMode.And)
        {
            // ALL must pass
            foreach (var condition in Conditions)
            {
                try
                {
                    var result = await condition.ExecuteAsync(context, ct);
                    if (!result.Success) return false;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[StateTransition] AND condition failed → Target='{ToState}', Priority={Priority}, Error={ex.Message}");
                    return false;
                }
            }
            return true;
        }

        if (LogicMode == ConditionLogicMode.Or)
        {
            // ANY must pass
            foreach (var condition in Conditions)
            {
                try
                {
                    var result = await condition.ExecuteAsync(context, ct);
                    if (result.Success) return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[StateTransition] OR condition failed → Target='{ToState}', Priority={Priority}, Error={ex.Message}");
                    // Continue to next condition
                }
            }
            return false;
        }

        return true;
    }
}

