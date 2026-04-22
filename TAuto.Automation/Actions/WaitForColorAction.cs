using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

[ActionMetadata("Wait For Color", "Vision & OCR", "🎨", IsAdvanced = true)]
public class WaitForColorAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name) ? $"⏳ {Name}" : $"⏳ Wait for Color ({TargetColorHex})";

    [ActionParameter("Name", "Friendly name for this action.")]
    public string Name { get; set; } = string.Empty;

    [ActionParameter("Target Color", "Hex color code (e.g., #FF0000).")]
    public string TargetColorHex { get; set; } = "#FFFFFF";

    [ActionParameter("Tolerance", "Color match tolerance (0-255).")]
    public int Tolerance { get; set; } = 10;

    [ActionParameter("Timeout (ms)", "Max time to wait for color.")]
    public int TimeoutMs { get; set; } = 10000;

    public int RetryInterval { get; set; } = 500;
    public bool FailOnTimeout { get; set; } = true;

    [ActionParameter("Result Var", "Variable to store boolean result.", IsAdvanced = true)]
    public string ResultVariableName { get; set; } = string.Empty;
    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public int RegionWidth { get; set; }
    public int RegionHeight { get; set; }
    public int MinPixelCount { get; set; } = 1;

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (!TryParseColor(TargetColorHex, out var color))
            return ActionResult.Fail($"Invalid color: {TargetColorHex}");

        Rectangle? region = RegionWidth > 0 && RegionHeight > 0
            ? new Rectangle(RegionX, RegionY, RegionWidth, RegionHeight)
            : null;

        DateTime startTime = DateTime.Now;
        while (!ct.IsCancellationRequested)
        {
            await context.UpdateScreenCaptureAsync(force: true);
            if (context.LastScreenCapture != null)
            {
                var result = await context.Vision.FindColorAsync(context.LastScreenCapture, new ColorSearchOptions
                {
                    TargetColor = color,
                    Tolerance = Tolerance,
                    SearchRegion = region,
                    MinPixelCount = MinPixelCount
                });

                if (result.Found)
                {
                    context.LastFoundImageLocation = result.CenterLocation;
                    if (!string.IsNullOrEmpty(ResultVariableName))
                        context.SetVariable(ResultVariableName, true);
                    return ActionResult.Ok(result.CenterLocation);
                }
            }

            if ((DateTime.Now - startTime).TotalMilliseconds >= TimeoutMs)
            {
                if (!string.IsNullOrEmpty(ResultVariableName))
                    context.SetVariable(ResultVariableName, false);
                return FailOnTimeout ? ActionResult.Fail($"Color not found after {TimeoutMs}ms") : ActionResult.Ok();
            }

            await Task.Delay(RetryInterval, ct);
        }

        return ActionResult.Fail("Cancelled");
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            color = ColorTranslator.FromHtml(value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
