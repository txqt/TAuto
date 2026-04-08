using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;
using TAuto.Core.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Conditional action that checks if an image exists on screen.
/// </summary>
[ActionMetadata("If Image Found", "Logic", "I")]
public class IfImageFoundAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"If: {Name}"
        : $"If Image Found ({Threshold:P0})";

    private string _name = string.Empty;

    [ActionParameter("Name", "Optional friendly name shown in the editor.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _templatePath = string.Empty;

    [ActionParameter("Template Path", "Path to the PNG template used for image matching.", EditorType = ActionParameterEditorType.ImagePath)]
    public string TemplatePath
    {
        get => _templatePath;
        set => SetProperty(ref _templatePath, value);
    }

    private double _threshold = 0.8;

    [ActionParameter("Threshold", "Image match confidence threshold between 0.0 and 1.0.")]
    public double Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, value);
    }

    private string _thenActionId = string.Empty;

    [ActionParameter("Then Action", "Action to jump to when the image is found.", EditorType = ActionParameterEditorType.ActionId)]
    public string ThenActionId
    {
        get => _thenActionId;
        set => SetProperty(ref _thenActionId, value);
    }

    private string _elseActionId = string.Empty;

    [ActionParameter("Else Action", "Action to jump to when the image is not found.", EditorType = ActionParameterEditorType.ActionId)]
    public string ElseActionId
    {
        get => _elseActionId;
        set => SetProperty(ref _elseActionId, value);
    }

    private bool _forceFreshCapture = true;

    [ActionParameter("Fresh Capture", "Capture the latest frame before matching.", IsAdvanced = true)]
    public bool ForceFreshCapture
    {
        get => _forceFreshCapture;
        set => SetProperty(ref _forceFreshCapture, value);
    }

    private bool _storeLocation = true;

    [ActionParameter("Store Location", "Store the last found coordinates in the script context.", IsAdvanced = true)]
    public bool StoreLocation
    {
        get => _storeLocation;
        set => SetProperty(ref _storeLocation, value);
    }

    private int _consecutiveFrames = 1;

    [ActionParameter("Consecutive Frames", "Require the image to be confirmed over multiple frames.", IsAdvanced = true)]
    public int ConsecutiveFrames
    {
        get => _consecutiveFrames;
        set => SetProperty(ref _consecutiveFrames, value);
    }

    private bool _disableMultiScale;

    [ActionParameter("Disable Multi-Scale", "Skip multi-scale matching optimizations.", IsAdvanced = true)]
    public bool DisableMultiScale
    {
        get => _disableMultiScale;
        set => SetProperty(ref _disableMultiScale, value);
    }

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

        var confirmation = new TAuto.Automation.Utilities.DetectionConfirmation(ConsecutiveFrames);
        TemplateMatchResult? lastMatch = null;
        var maxAttempts = ConsecutiveFrames == 1 ? 1 : ConsecutiveFrames + 5;

        for (var i = 0; i < maxAttempts; i++)
        {
            await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
            if (context.LastScreenCapture == null)
            {
                return ActionResult.Fail("Cannot capture screen");
            }

            var result = context.GetCachedMatch(TemplatePath, Threshold);
            if (result == null)
            {
                result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath, disableMultiScale: DisableMultiScale);
                context.CacheMatch(TemplatePath, Threshold, result);
            }

            if (result.Found)
            {
                lastMatch = result;
            }

            if (confirmation.RecordResult(result.Found))
            {
                if (StoreLocation && lastMatch != null)
                {
                    context.LastFoundImageLocation = lastMatch.CenterLocation;
                }

                return !string.IsNullOrEmpty(ThenActionId) ? ActionResult.Jump(ThenActionId) : ActionResult.Ok();
            }

            if (ConsecutiveFrames > 1 && i < maxAttempts - 1)
            {
                await Task.Delay(100, ct);
            }
        }

        context.LastFoundImageLocation = null;
        return !string.IsNullOrEmpty(ElseActionId)
            ? ActionResult.Jump(ElseActionId)
            : ActionResult.Fail("Image not found");
    }
}
