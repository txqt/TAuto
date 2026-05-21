using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;
using Microsoft.Extensions.Logging;
using TAuto.Automation.Actions;
using TAuto.Automation.BotSystem;

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
    /// User-supplied argument values, populated by the host before RunAsync.
    /// Delegated to Configuration.
    /// </summary>
    public Dictionary<string, object> Arguments => Configuration.Arguments;

    public ILogger<BotBase>? Logger { get; set; }
    public IRetryPolicy RetryPolicy { get; set; } = new DefaultRetryPolicy();
    private IBotConfiguration _configuration = null!;
    public IBotConfiguration Configuration 
    { 
        get => _configuration;
        set
        {
            if (_configuration != null) _configuration.OnArgumentFallback -= Configuration_OnArgumentFallback;
            _configuration = value ?? new DefaultBotConfiguration();
            _configuration.OnArgumentFallback += Configuration_OnArgumentFallback;
        }
    }

    private void Configuration_OnArgumentFallback(string msg)
    {
        Log(msg);
    }
    public IBotPausable Pausable { get; }
    public IVisionHelper Vision { get; }
    public IGameLifecycle Lifecycle { get; }

    protected BotBase()
    {
        Configuration = new DefaultBotConfiguration();
        Pausable = new DefaultBotPausable(() => Logger);
        Vision = new DefaultVisionHelper(() => Context, () => CancellationToken, () => Pausable, () => Logger);
        Lifecycle = new DefaultGameLifecycle(() => Context, () => Logger);
    }

    /// <summary>
    /// Reference resolution at which coordinates and templates were measured.
    /// Override in bot constructor if your templates were captured at a different resolution.
    /// Default: 1280×720.
    /// </summary>
    public (int Width, int Height) ReferenceResolution { get; set; } = (1280, 720);

    /// <summary>
    /// Optional game health monitor. Set in bot constructor to enable
    /// automatic crash/freeze detection.
    /// </summary>
    public GameHealthMonitor? HealthMonitor { get; set; }

    /// <summary>
    /// Package name of the game being automated (e.g., "com.lilithgame.roc.gp").
    /// Delegated to Lifecycle.
    /// </summary>
    public string? GamePackageName { get => Lifecycle.GamePackageName; set => Lifecycle.GamePackageName = value; }

    public void Initialize(ScriptContext context, CancellationToken token)
    {
        Context = context;
        CancellationToken = token;
        
        if (HealthMonitor != null)
        {
            Context.HealthMonitor = HealthMonitor;
        }
    }

    /// <summary>
    /// Sets the argument values from the host (UI form or CLI).
    /// </summary>
    public void SetArguments(Dictionary<string, object> args)
    {
        Configuration.SetArguments(args);
    }

    /// <summary>
    /// Override to declare the bot's configuration: name, description,
    /// run mode (Standard/CustomUI/CLI), and configurable arguments.
    /// Returns null by default (Standard mode, no arguments).
    /// </summary>
    public IUserInterfaceAdapter UI { get; set; } = new Services.ConsoleInteractionService();

    public virtual BotConfiguration? GetConfiguration() => null;

    /// <summary>
    /// Called by the host when RunMode is CustomUI, right before RunAsync.
    /// Override to create and show a custom WPF Window.
    /// The host passes itself so the bot can interact with it.
    /// </summary>
    /// <returns>The Window or UserControl to display. Null to skip.</returns>
    public virtual object? OnCreateUI() => null;

    /// <summary>
    /// Called by the host when RunMode is CLI, right before RunAsync.
    /// Override to write console-based UI (e.g. menus, progress bars).
    /// A console window will already be allocated by the host.
    /// </summary>
    public virtual void OnCreateConsole() { }

    /// <summary>
    /// Displays an interactive menu.
    /// Returns the index of the selected option.
    /// </summary>
    public virtual Task<int> ShowMenu(string title, params string[] options)
    {
        return UI.ShowMenuAsync(title, options);
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
            int x = (int)(Context.LastScreenCapture.Width * xPercent / 100);
            int y = (int)(Context.LastScreenCapture.Height * yPercent / 100);
            await Context.Device.TapAsync(x, y);
        }
    }


    protected async Task Swipe(int x1, int y1, int x2, int y2, int durationMs = 300)
    {
        CheckCancelled();
        await Context.Device.SwipeAsync(x1, y1, x2, y2, durationMs);
    }

    protected async Task LongPress(int x, int y, int durationMs = 500)
    {
        CheckCancelled();
        await Context.Device.LongPressAsync(x, y, durationMs);
    }

    #endregion



    #region Helper Methods - Utility

    protected async Task Delay(int ms)
    {
        CheckCancelled(); 
        await Pausable.CheckPausedAsync(); // Wait if paused

        try
        {
            await Task.Delay(ms, CancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw; 
        }

        await Pausable.CheckPausedAsync(); // Wait again in case we were paused during delay? 
        // Usually checking before is enough for the next action, but checking after ensures we don't proceed if paused exactly when waking up.
    }

    public static event Action<string>? OnLogReceived;

    protected void Log(string message)
    {
        OnLogReceived?.Invoke(message);
        UI.WriteMessage($"[Bot] {message}");
        Logger?.LogInformation(message);
    }

    protected Task<bool> Retry(Func<Task<bool>> action, int maxRetries = 3, int intervalMs = 1000)
    {
        return RetryPolicy.RetryAsync(action, maxRetries, intervalMs, CancellationToken);
    }

    protected void CheckCancelled()
    {
        CancellationToken.ThrowIfCancellationRequested();
    }

    #endregion

    #region Helper Methods - Arguments

    /// <summary>
    /// Gets an argument value by name, with a default fallback.
    /// Safely handles System.Text.Json.JsonElement from IPC.
    /// </summary>
    protected T GetArg<T>(string name, T defaultValue = default!) => Configuration.GetArg(name, defaultValue);
    protected string GetArgString(string name, string defaultValue = "") => Configuration.GetArgString(name, defaultValue);
    protected int GetArgInt(string name, int defaultValue = 0) => Configuration.GetArgInt(name, defaultValue);
    protected bool GetArgBool(string name, bool defaultValue = false) => Configuration.GetArgBool(name, defaultValue);
    protected double GetArgDouble(string name, double defaultValue = 0.0) => Configuration.GetArgDouble(name, defaultValue);

    #endregion
}
