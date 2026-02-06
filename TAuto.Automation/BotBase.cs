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

    public void Initialize(ScriptContext context, CancellationToken token)
    {
        Context = context;
        CancellationToken = token;
    }

    public abstract Task RunAsync();

    #region Helper Methods - Interaction

    protected async Task Tap(int x, int y)
    {
        CheckCancelled();
        await Context.Device.TapAsync(x, y);
    }

    protected async Task TapPercent(double xPercent, double yPercent)
    {
        CheckCancelled();
        // Resolve resolution if needed
        await Context.UpdateScreenCaptureAsync(force: true);

        if (Context.LastScreenCapture != null)
        {
            int x = (int)(Context.LastScreenCapture.PixelWidth * xPercent / 100);
            int y = (int)(Context.LastScreenCapture.PixelHeight * yPercent / 100);
            await Context.Device.TapAsync(x, y);
        }
    }

    protected async Task Swipe(int x1, int y1, int x2, int y2, int durationMs = 300)
    {
        CheckCancelled();
        await Context.Device.SwipeAsync(x1, y1, x2, y2, durationMs);
    }

    #endregion

    #region Helper Methods - Vision

    protected async Task<System.Windows.Point?> FindImage(string templatePath, double threshold = 0.8)
    {
        CheckCancelled();
        // Ensure we have a fresh capture
        await Context.UpdateScreenCaptureAsync();
        
        if (Context.LastScreenCapture == null) return null;

        // Load template
        var template = Context.Vision.LoadTemplate(templatePath);
        if (template == null)
        {
            Log($"Warning: Template not found at path '{templatePath}'");
            return null;
        }

        var result = Context.Vision.FindTemplate(Context.LastScreenCapture, template, threshold);
        
        if (result.Found)
        {
            Context.LastFoundImageLocation = result.CenterLocation;
            return result.CenterLocation;
        }
        return null;
    }

    protected async Task<bool> Exists(string templatePath, double threshold = 0.8)
    {
        return (await FindImage(templatePath, threshold)) != null;
    }

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

    protected async Task Delay(int ms)
    {
        CheckCancelled(); 
        try
        {
            await Task.Delay(ms, CancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw; 
        }
    }

    protected void Log(string message)
    {
        Console.WriteLine($"[Bot] {message}");
    }

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
