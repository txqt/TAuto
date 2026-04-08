using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;
using TAuto.Core.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that finds an image template on the screen and stores the last found location.
/// </summary>
[ActionMetadata("Find Image", "Vision", "F")]
public class FindImageAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"Find {Name}"
        : $"Find Image ({Threshold:P0})";

    [ActionParameter("Name", "Optional friendly name shown in the editor.")]
    public string Name { get; set; } = string.Empty;

    [ActionParameter("Template Path", "Path to the PNG template used for image matching.", EditorType = ActionParameterEditorType.ImagePath)]
    public string TemplatePath { get; set; } = string.Empty;

    [ActionParameter("Threshold", "Image match confidence threshold between 0.0 and 1.0.")]
    public double Threshold { get; set; } = 0.8;

    [ActionParameter("Fresh Capture", "Capture the latest frame before matching.", IsAdvanced = true)]
    public bool ForceFreshCapture { get; set; } = true;

    [ActionParameter("Result Variable", "Optional variable name to store whether the image was found.", IsAdvanced = true)]
    public string ResultVariableName { get; set; } = string.Empty;

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return ActionResult.Fail("Cancelled");
        }

        if (string.IsNullOrEmpty(TemplatePath))
        {
            return ActionResult.Fail("Template path not set");
        }

        string? baseDir = context.GetString("BaseDirectory");
        IImage? template = context.Vision.LoadTemplate(TemplatePath, baseDir);
        if (template == null)
        {
            return ActionResult.Fail($"Cannot load template: {TemplatePath}");
        }

        await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
        if (context.LastScreenCapture == null)
        {
            return ActionResult.Fail("Cannot capture screen");
        }

        var result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath);
        context.LastFoundImageLocation = result.Found ? result.CenterLocation : null;

        if (!string.IsNullOrEmpty(ResultVariableName))
        {
            context.SetVariable(ResultVariableName, result.Found);
        }

        return result.Found ? ActionResult.Ok(result.CenterLocation) : ActionResult.Ok();
    }
}
