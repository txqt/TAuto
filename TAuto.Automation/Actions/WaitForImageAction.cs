using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;
using TAuto.Core.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that waits until a specific image appears on screen.
/// </summary>
[ActionMetadata("Wait For Image", "Vision", "W")]
public class WaitForImageAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"Wait: {Name}"
        : $"Wait for Image ({TimeoutMs}ms)";

    private string _name = string.Empty;

    [ActionParameter("Name", "Optional friendly name shown in the editor.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _templatePath = string.Empty;

    [ActionParameter("Template Path", "Path to the PNG template used for image matching.")]
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

    private int _timeoutMs = 10000;

    [ActionParameter("Timeout (ms)", "Maximum time to wait before timing out.")]
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => SetProperty(ref _timeoutMs, value);
    }

    private int _retryInterval = 500;

    [ActionParameter("Retry Interval (ms)", "Delay between capture attempts.", IsAdvanced = true)]
    public int RetryInterval
    {
        get => _retryInterval;
        set => SetProperty(ref _retryInterval, value);
    }

    private bool _failOnTimeout = true;

    [ActionParameter("Fail On Timeout", "Return a failed result if the image never appears.")]
    public bool FailOnTimeout
    {
        get => _failOnTimeout;
        set => SetProperty(ref _failOnTimeout, value);
    }

    private string _resultVariableName = string.Empty;

    [ActionParameter("Result Variable", "Optional variable name to store whether the image was found.", IsAdvanced = true)]
    public string ResultVariableName
    {
        get => _resultVariableName;
        set => SetProperty(ref _resultVariableName, value);
    }

    private int _consecutiveFrames = 1;

    [ActionParameter("Consecutive Frames", "Require the image to be confirmed over multiple frames.", IsAdvanced = true)]
    public int ConsecutiveFrames
    {
        get => _consecutiveFrames;
        set => SetProperty(ref _consecutiveFrames, value);
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

        var startTime = DateTime.Now;
        var confirmation = new TAuto.Automation.Utilities.DetectionConfirmation(ConsecutiveFrames);
        TemplateMatchResult? matchResult = null;

        while (!ct.IsCancellationRequested)
        {
            await context.UpdateScreenCaptureAsync(force: true);
            if (context.LastScreenCapture == null)
            {
                await Task.Delay(RetryInterval, ct);
                continue;
            }

            var result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath);
            if (result.Found)
            {
                matchResult = result;
            }

            if (confirmation.RecordResult(result.Found))
            {
                context.LastFoundImageLocation = matchResult!.CenterLocation;
                if (!string.IsNullOrEmpty(ResultVariableName))
                {
                    context.SetVariable(ResultVariableName, true);
                }

                return ActionResult.Ok(matchResult.CenterLocation);
            }

            if ((DateTime.Now - startTime).TotalMilliseconds >= TimeoutMs)
            {
                if (!string.IsNullOrEmpty(ResultVariableName))
                {
                    context.SetVariable(ResultVariableName, false);
                }

                return FailOnTimeout
                    ? ActionResult.Fail($"Image not found after {TimeoutMs}ms")
                    : ActionResult.Ok();
            }

            await Task.Delay(RetryInterval, ct);
        }

        return ActionResult.Fail("Cancelled");
    }
}
