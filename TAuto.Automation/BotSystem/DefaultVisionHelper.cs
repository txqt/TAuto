using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TAuto.Core;
using TAuto.Core.Imaging;

namespace TAuto.Automation.BotSystem;

public class DefaultVisionHelper : IVisionHelper
{
    private readonly Func<ScriptContext> _contextProvider;
    private readonly Func<CancellationToken> _tokenProvider;
    private readonly Func<IBotPausable> _pausableProvider;
    private readonly Func<ILogger?> _loggerProvider;

    public DefaultVisionHelper(
        Func<ScriptContext> contextProvider, 
        Func<CancellationToken> tokenProvider,
        Func<IBotPausable> pausableProvider,
        Func<ILogger?> loggerProvider)
    {
        _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _pausableProvider = pausableProvider ?? throw new ArgumentNullException(nameof(pausableProvider));
        _loggerProvider = loggerProvider ?? throw new ArgumentNullException(nameof(loggerProvider));
    }

    private ScriptContext Context => _contextProvider();
    private CancellationToken Token => _tokenProvider();
    private IBotPausable Pausable => _pausableProvider();
    private ILogger? Logger => _loggerProvider();

    private void CheckCancelled() => Token.ThrowIfCancellationRequested();

    private async Task Delay(int ms)
    {
        CheckCancelled(); 
        await Pausable.CheckPausedAsync(); 
        try { await Task.Delay(ms, Token); } catch (TaskCanceledException) { throw; }
        await Pausable.CheckPausedAsync();
    }

    public async Task<Point?> FindImageAsync(string templatePath, double threshold = 0.8)
    {
        CheckCancelled();
        await Context.UpdateScreenCaptureAsync();
        
        if (Context.LastScreenCapture == null) return null;

        string? baseDir = Context.GetString("BaseDirectory");
        var template = Context.Vision.LoadTemplate(templatePath, baseDir);
        if (template == null)
        {
            Logger?.LogWarning($"Warning: Template not found at path '{templatePath}'");
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

    public async Task<bool> ExistsAsync(string templatePath, double threshold = 0.8)
    {
        return (await FindImageAsync(templatePath, threshold)) != null;
    }

    public async Task<bool> ClickImageAsync(string templatePath, double threshold = 0.8, int offsetX = 0, int offsetY = 0)
    {
        var point = await FindImageAsync(templatePath, threshold);
        if (point.HasValue)
        {
            int x = (int)point.Value.X + offsetX;
            int y = (int)point.Value.Y + offsetY;
            CheckCancelled();
            await Context.Device.TapAsync(x, y);
            return true;
        }
        return false;
    }

    public async Task<bool> WaitForImageAsync(string templatePath, int timeoutMs = 5000, double threshold = 0.8)
    {
        var start = DateTime.Now;
        while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
        {
            if (await ExistsAsync(templatePath, threshold))
                return true;

            await Delay(500);
        }
        return false;
    }

    public async Task<Point?> FindColorAsync(Color color, int tolerance = 10, Rectangle? region = null, int minPixelCount = 1)
    {
        CheckCancelled();
        await Context.UpdateScreenCaptureAsync();

        if (Context.LastScreenCapture == null) return null;

        var result = Context.Vision.FindColor(Context.LastScreenCapture, new ColorSearchOptions
        {
            TargetColor = color,
            Tolerance = tolerance,
            SearchRegion = region,
            MinPixelCount = minPixelCount
        });

        if (result.Found)
        {
            Context.LastFoundImageLocation = result.CenterLocation;
            return result.CenterLocation;
        }

        return null;
    }

    public async Task<bool> WaitForColorAsync(Color color, int timeoutMs = 5000, int tolerance = 10, Rectangle? region = null, int minPixelCount = 1)
    {
        var start = DateTime.Now;
        while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
        {
            if ((await FindColorAsync(color, tolerance, region, minPixelCount)) != null)
                return true;

            await Delay(500);
        }
        return false;
    }

    public TemplateMatchResult[] FindTemplates(IImage source,
        (IImage Template, string? Path, double Threshold)[] templates,
        Rectangle? roi = null,
        Rectangle[]? regions = null,
        bool fallbackFullscreen = false,
        bool disableMultiScale = false)
    {
        CheckCancelled();
        return Context.Vision.FindTemplates(source, templates, roi, regions, fallbackFullscreen, disableMultiScale);
    }
}
