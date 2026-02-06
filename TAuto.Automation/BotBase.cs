using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;
using TAuto.Automation.Actions;

namespace TAuto.Automation;

/// <summary>
/// Base class for all Code-First Bots.
/// Provides fluent API for automation.
/// </summary>
public abstract class BotBase
{
    public ScriptContext Context { get; private set; } = null!;
    public CancellationToken CancellationToken { get; private set; }

    /// <summary>
    /// Initialize the bot with context and token.
    /// Called by the runner before RunAsync.
    /// </summary>
    public void Initialize(ScriptContext context, CancellationToken token)
    {
        Context = context;
        CancellationToken = token;
    }

    /// <summary>
    /// Main logic of the bot. Override this to implement your behavior.
    /// </summary>
    public abstract Task RunAsync();

    #region Helper Methods - Interaction

    /// <summary>
    /// Tap at specific coordinates.
    /// </summary>
    protected async Task Tap(int x, int y)
    {
        CheckCancelled();
        await Context.Device.TapAsync(x, y);
    }

    /// <summary>
    /// Tap at a percentage of the screen (0-100).
    /// </summary>
    protected async Task TapPercent(double xPercent, double yPercent)
    {
        CheckCancelled();
        // Resolve resolution if needed
        if (Context.LastScreenCapture == null)
            await Context.UpdateScreenCaptureAsync(force: true);

        if (Context.LastScreenCapture != null)
        {
            int x = (int)(Context.LastScreenCapture.PixelWidth * xPercent / 100);
            int y = (int)(Context.LastScreenCapture.PixelHeight * yPercent / 100);
            await Context.Device.TapAsync(x, y);
        }
    }

    /// <summary>
    /// Input text.
    /// </summary>
    protected async Task InputText(string text)
    {
        CheckCancelled();
        await Context.Device.InputTextAsync(text);
    }

    /// <summary>
    /// Press a key code.
    /// </summary>
    protected async Task PressKey(int keyCode)
    {
        CheckCancelled();
        await Context.Device.InputKeyEventAsync(keyCode);
    }

    /// <summary>
    /// Swipe from start to end coordinates.
    /// </summary>
    protected async Task Swipe(int x1, int y1, int x2, int y2, int durationMs = 300)
    {
        CheckCancelled();
        await Context.Device.SwipeAsync(x1, y1, x2, y2, durationMs);
    }

    #endregion

    #region Helper Methods - Vision

    /// <summary>
    /// Find an image on screen. Returns center point if found, null otherwise.
    /// </summary>
    protected async Task<System.Windows.Point?> FindImage(string templatePath, double threshold = 0.8)
    {
        CheckCancelled();
        // Ensure we have a fresh capture
        await Context.UpdateScreenCaptureAsync();
        
        if (Context.LastScreenCapture == null) return null;

        var result = Context.Vision.FindImage(Context.LastScreenCapture, templatePath, threshold);
        if (result.IsFound)
        {
            Context.LastFoundImageLocation = result.Center;
            return result.Center;
        }
        return null;
    }

    /// <summary>
    /// Check if an image exists on screen.
    /// </summary>
    protected async Task<bool> Exists(string templatePath, double threshold = 0.8)
    {
        return (await FindImage(templatePath, threshold)) != null;
    }

    /// <summary>
    /// Find and click an image. Returns true if clicked.
    /// </summary>
    protected async Task<bool> ClickImage(string templatePath, double threshold = 0.8, int offsetX = 0, int offsetY = 0)
    {
        var point = await FindImage(templatePath, threshold);
        if (point.HasValue)
        {
            int x = (int)point.Value.X + offsetX;
            int y = (int)point.Value.Y + offsetY;
            await Tap(x, y);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Wait until image appears.
    /// </summary>
    protected async Task<bool> WaitForImage(string templatePath, int timeoutMs = 5000, double threshold = 0.8)
    {
        var start = DateTime.Now;
        while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
        {
            if (await Exists(templatePath, threshold))
                return true;
            
            await Delay(500);
        }
        return false;
    }

    #endregion

    #region Helper Methods - Utility

    /// <summary>
    /// Delay execution.
    /// </summary>
    protected async Task Delay(int ms)
    {
        CheckCancelled(); // Check before
        try
        {
            await Task.Delay(ms, CancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw; // Propagate cancellation
        }
    }

    /// <summary>
    /// Log a message.
    /// </summary>
    protected void Log(string message)
    {
        // TODO: Hook into Context logging event if available, for now console
        // We really should add a Log method to ScriptContext or expose an Event
        Console.WriteLine($"[Bot] {message}");
        // Context itself doesn't have a logger, but we can raise an event via a variable or a new mechanic.
        // For now, let's assume we can add a Log method to ScriptContext later.
    }

    /// <summary>
    /// Utility to retry an action.
    /// </summary>
    protected async Task<bool> Retry(Func<Task<bool>> action, int maxRetries = 3, int intervalMs = 1000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            if (await action()) return true;
            await Delay(intervalMs);
        }
        return false;
    }

    protected void CheckCancelled()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }

    #endregion
}
