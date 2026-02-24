using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Click-and-Confirm action: clicks an image, then verifies the UI changed.
/// Retries the click if the original image is still visible (game didn't register the click).
/// 
/// Verification modes:
/// - WaitForGone: After clicking, wait for the clicked image to DISAPPEAR.
/// - WaitForNext: After clicking, wait for a DIFFERENT image to APPEAR.
/// </summary>
public class ReliableClickAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"🔒 Reliable Click: {Name}"
        : $"🔒 Reliable Click ({TemplatePath})";

    // ===== Configuration =====

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _templatePath = string.Empty;
    /// <summary>
    /// Path to the image to click.
    /// </summary>
    public string TemplatePath
    {
        get => _templatePath;
        set => SetProperty(ref _templatePath, value);
    }

    private double _threshold = 0.8;
    public double Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, value);
    }

    private int _offsetX = 0;
    public int OffsetX
    {
        get => _offsetX;
        set => SetProperty(ref _offsetX, value);
    }

    private int _offsetY = 0;
    public int OffsetY
    {
        get => _offsetY;
        set => SetProperty(ref _offsetY, value);
    }

    private int _randomOffset = 3;
    /// <summary>
    /// Random pixel offset for anti-detection (default 3px).
    /// </summary>
    public int RandomOffset
    {
        get => _randomOffset;
        set => SetProperty(ref _randomOffset, value);
    }

    private int _maxRetries = 3;
    /// <summary>
    /// Maximum number of click attempts before giving up.
    /// </summary>
    public int MaxRetries
    {
        get => _maxRetries;
        set => SetProperty(ref _maxRetries, value);
    }

    private int _confirmTimeoutMs = 2000;
    /// <summary>
    /// How long to wait for state change confirmation after each click (ms).
    /// </summary>
    public int ConfirmTimeoutMs
    {
        get => _confirmTimeoutMs;
        set => SetProperty(ref _confirmTimeoutMs, value);
    }

    private int _confirmCheckIntervalMs = 300;
    /// <summary>
    /// How often to re-check the screen during confirmation (ms).
    /// </summary>
    public int ConfirmCheckIntervalMs
    {
        get => _confirmCheckIntervalMs;
        set => SetProperty(ref _confirmCheckIntervalMs, value);
    }

    private ConfirmMode _confirmMode = ConfirmMode.WaitForGone;
    /// <summary>
    /// How to verify the click was registered.
    /// </summary>
    public ConfirmMode Confirm
    {
        get => _confirmMode;
        set => SetProperty(ref _confirmMode, value);
    }

    private string _confirmTemplatePath = string.Empty;
    /// <summary>
    /// (WaitForNext mode only) Path to the image that should appear after a successful click.
    /// </summary>
    public string ConfirmTemplatePath
    {
        get => _confirmTemplatePath;
        set => SetProperty(ref _confirmTemplatePath, value);
    }

    private int _delayAfterMs = 300;
    public int DelayAfterMs
    {
        get => _delayAfterMs;
        set => SetProperty(ref _delayAfterMs, value);
    }

    private int _findTimeoutMs = 5000;
    /// <summary>
    /// Maximum time to wait for the initial image to appear before first click (ms).
    /// </summary>
    public int FindTimeoutMs
    {
        get => _findTimeoutMs;
        set => SetProperty(ref _findTimeoutMs, value);
    }

    // ===== Execute =====

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        if (string.IsNullOrEmpty(context.TargetId)) return ActionResult.Fail("No device connected");
        if (string.IsNullOrEmpty(TemplatePath)) return ActionResult.Fail("Template path not set");

        string? baseDir = context.GetString("BaseDirectory");
        IImage? template = context.Vision.LoadTemplate(TemplatePath, baseDir);
        if (template == null) return ActionResult.Fail($"Cannot load template: {TemplatePath}");

        IImage? confirmTemplate = null;
        if (Confirm == ConfirmMode.WaitForNext && !string.IsNullOrEmpty(ConfirmTemplatePath))
        {
            confirmTemplate = context.Vision.LoadTemplate(ConfirmTemplatePath, baseDir);
            if (confirmTemplate == null) return ActionResult.Fail($"Cannot load confirm template: {ConfirmTemplatePath}");
        }

        var rnd = new Random();

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");

            // ── Step 1: Find the target image ──
            TemplateMatchResult? match = null;
            DateTime findStart = DateTime.Now;
            do
            {
                await context.UpdateScreenCaptureAsync(force: true);
                if (context.LastScreenCapture == null) return ActionResult.Fail("Cannot capture screen");

                match = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath);
                if (match.Found) break;

                if ((DateTime.Now - findStart).TotalMilliseconds >= FindTimeoutMs)
                    return ActionResult.Fail($"Image not found after {FindTimeoutMs}ms (attempt {attempt}/{MaxRetries})");

                await Task.Delay(ConfirmCheckIntervalMs, ct);
            } while (!ct.IsCancellationRequested);

            if (match == null || !match.Found) return ActionResult.Fail("Image not found");

            // ── Step 2: Click with random offset ──
            int tapX = (int)match.CenterLocation.X + OffsetX;
            int tapY = (int)match.CenterLocation.Y + OffsetY;
            if (RandomOffset > 0)
            {
                tapX += rnd.Next(-RandomOffset, RandomOffset + 1);
                tapY += rnd.Next(-RandomOffset, RandomOffset + 1);
            }

            context.LastFoundImageLocation = new System.Drawing.Point(tapX, tapY);
            bool tapResult = await context.Device.TapAsync(tapX, tapY);
            if (!tapResult) return ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");

            // ── Step 3: Confirm state change ──
            bool confirmed = await ConfirmStateChange(context, ct, template, confirmTemplate);
            if (confirmed)
            {
                if (DelayAfterMs > 0) await Task.Delay(DelayAfterMs, ct);
                return ActionResult.Ok(match.CenterLocation);
            }

            // Not confirmed — retry
            System.Diagnostics.Debug.WriteLine($"[ReliableClick] Attempt {attempt}/{MaxRetries} — click not confirmed, retrying...");
            await Task.Delay(ConfirmCheckIntervalMs, ct);
        }

        return ActionResult.Fail($"Click not confirmed after {MaxRetries} attempts");
    }

    private async Task<bool> ConfirmStateChange(
        ScriptContext context, CancellationToken ct,
        IImage clickedTemplate, IImage? nextTemplate)
    {
        DateTime confirmStart = DateTime.Now;

        while ((DateTime.Now - confirmStart).TotalMilliseconds < ConfirmTimeoutMs && !ct.IsCancellationRequested)
        {
            await Task.Delay(ConfirmCheckIntervalMs, ct);
            await context.UpdateScreenCaptureAsync(force: true);
            if (context.LastScreenCapture == null) continue;

            if (Confirm == ConfirmMode.WaitForGone)
            {
                // Success = clicked image is NO LONGER visible
                var result = context.Vision.FindTemplate(context.LastScreenCapture, clickedTemplate, Threshold, TemplatePath);
                if (!result.Found) return true;
            }
            else if (Confirm == ConfirmMode.WaitForNext && nextTemplate != null)
            {
                // Success = confirm image IS NOW visible
                var result = context.Vision.FindTemplate(context.LastScreenCapture, nextTemplate, Threshold, ConfirmTemplatePath);
                if (result.Found)
                {
                    context.LastFoundImageLocation = result.CenterLocation;
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// How ReliableClickAction confirms that the click registered.
/// </summary>
public enum ConfirmMode
{
    /// <summary>Wait for the clicked image to disappear.</summary>
    WaitForGone,

    /// <summary>Wait for a different (confirm) image to appear.</summary>
    WaitForNext
}
