using System.Threading;
using System.Threading.Tasks;

namespace TAuto.Core;

/// <summary>
/// Interface for executable actions in scripts.
/// </summary>
public interface IAction
{
    /// <summary>
    /// Unique identifier for this action instance.
    /// </summary>
    string Id { get; set; }
    
    /// <summary>
    /// Display name for UI.
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// Execute the action.
    /// </summary>
    Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct);

    /// <summary>
    /// Whether execution should pause before this action.
    /// </summary>
    bool IsBreakpoint { get; set; }

    /// <summary>
    /// Number of times to retry if the action fails.
    ///Default is 0.
    /// </summary>
    int RetryCount { get; set; }

    /// <summary>
    /// Interval in milliseconds to wait between retries.
    /// Default is 1000ms.
    /// </summary>
    int RetryIntervalMs { get; set; }

    /// <summary>
    /// If true, the script continues even if this action fails (after retries).
    /// </summary>
    bool ContinueOnError { get; set; }
}
