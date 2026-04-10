using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

[ActionMetadata("Find Color", "Vision & OCR", "🎨", IsAdvanced = true)]
public class FindColorAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name) ? $"🎨 {Name}" : $"🎨 Find Color ({TargetColorHex})";

    [ActionParameter("Name", "Friendly name for this action.")]
    public string Name { get; set; } = string.Empty;

    [ActionParameter("Target Color", "Hex color code (e.g., #FF0000).")]
    public string TargetColorHex { get; set; } = "#FFFFFF";

    [ActionParameter("Tolerance", "Color match tolerance (0-255).")]
    public int Tolerance { get; set; } = 10;

    [ActionParameter("Fresh Capture", "Capture latest frame.", IsAdvanced = true)]
    public bool ForceFreshCapture { get; set; } = true;

    [ActionParameter("Output Var", "Variable to store boolean result.", IsAdvanced = true)]
    public string ResultVariableName { get; set; } = string.Empty;
    public int RegionX { get; set; }
    public int RegionY { get; set; }
    public int RegionWidth { get; set; }
    public int RegionHeight { get; set; }
    public int MinPixelCount { get; set; } = 1;

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");

        if (!TryParseColor(TargetColorHex, out var color))
            return ActionResult.Fail($"Invalid color: {TargetColorHex}");

        await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
        if (context.LastScreenCapture == null)
            return ActionResult.Fail("Cannot capture screen");

        Rectangle? region = RegionWidth > 0 && RegionHeight > 0
            ? new Rectangle(RegionX, RegionY, RegionWidth, RegionHeight)
            : null;

        var result = context.Vision.FindColor(context.LastScreenCapture, new ColorSearchOptions
        {
            TargetColor = color,
            Tolerance = Tolerance,
            SearchRegion = region,
            MinPixelCount = MinPixelCount
        });

        context.LastFoundImageLocation = result.Found ? result.CenterLocation : null;

        if (!string.IsNullOrEmpty(ResultVariableName))
            context.SetVariable(ResultVariableName, result.Found);

        return result.Found ? ActionResult.Ok(result.CenterLocation) : ActionResult.Ok();
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
