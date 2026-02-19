using TAuto.Automation.Actions;
using TAuto.Core;

namespace TAuto.Automation.StateMachine;

/// <summary>
/// Fluent condition factory for state machine transitions.
/// Creates IAction-based conditions without verbose object initialization.
/// </summary>
public static class When
{
    /// <summary>
    /// Always true (unconditional transition).
    /// </summary>
    public static IAction? Always => null;

    /// <summary>
    /// True when the specified image template is found on screen.
    /// </summary>
    public static IAction ImageFound(string templatePath, double threshold = 0.8)
        => new IfImageFoundAction
        {
            TemplatePath = templatePath,
            Threshold = threshold
        };

    /// <summary>
    /// True when specified text is found via OCR on screen.
    /// </summary>
    public static IAction TextFound(string text, bool caseSensitive = false, bool partialMatch = true)
        => new IfTextFoundAction
        {
            TargetText = text,
            CaseSensitive = caseSensitive,
            PartialMatch = partialMatch
        };

    /// <summary>
    /// True when a context variable equals the expected value (string comparison).
    /// </summary>
    public static IAction Variable(string name, string expectedValue, string op = "==")
        => new IfVariableAction
        {
            VariableName = name,
            CompareValue = expectedValue,
            Operator = op
        };

    /// <summary>
    /// True when a boolean context variable is true.
    /// </summary>
    public static IAction IsTrue(string variableName)
        => new IfVariableAction
        {
            VariableName = variableName,
            CompareValue = "True",
            Operator = "=="
        };

    /// <summary>
    /// True when a boolean context variable is false.
    /// </summary>
    public static IAction IsFalse(string variableName)
        => new IfVariableAction
        {
            VariableName = variableName,
            CompareValue = "False",
            Operator = "=="
        };

    /// <summary>
    /// Custom inline condition using a delegate.
    /// Return ActionResult.Ok() for true, ActionResult.Fail() for false.
    /// </summary>
    public static IAction Condition(Func<ScriptContext, CancellationToken, Task<ActionResult>> check)
        => new DelegateAction(check);

    /// <summary>
    /// Synchronous inline condition.
    /// </summary>
    public static IAction Condition(Func<ScriptContext, bool> check)
        => new DelegateAction((ctx, ct) =>
            Task.FromResult(check(ctx) ? ActionResult.Ok() : ActionResult.Fail("")));

    /// <summary>
    /// Click an image template (acts as both action + condition: succeeds if found and clicked).
    /// </summary>
    public static IAction ClickImage(string templatePath, int delayAfterMs = 0, int timeoutMs = 10000, double threshold = 0.8)
        => new ClickImageAction
        {
            TemplatePath = templatePath,
            DelayAfterMs = delayAfterMs,
            TimeoutMs = timeoutMs,
            Threshold = threshold
        };
}
