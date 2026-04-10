using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that waits for a specified duration.
/// </summary>
[ActionMetadata("Delay", "Flow & Logic", "⌛")]
public class DelayAction : ActionBase
{
    public override string DisplayName => $"Delay {DelayMs}ms";

    [ActionParameter("Delay (ms)", "Base delay duration in milliseconds.")]
    public int DelayMs { get; set; } = 1000;

    [ActionParameter("Random Range (ms)", "Adds a random offset from -N to +N milliseconds.", IsAdvanced = true)]
    public int RandomMs { get; set; }

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return ActionResult.Fail("Cancelled");
        }

        var actualDelay = DelayMs;
        if (RandomMs > 0)
        {
            actualDelay += new Random().Next(-RandomMs, RandomMs + 1);
        }

        await Task.Delay(Math.Max(0, actualDelay), ct);
        return ActionResult.Ok();
    }
}
