using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;
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
    /// </summary>
    public Dictionary<string, object> Arguments { get; private set; } = new();

    private volatile TaskCompletionSource<bool>? _pauseSignal;
    public event Action<bool>? OnPausedStateChanged;

    public bool IsPaused => _pauseSignal != null;

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
    /// Used by RestartGameAsync and ForceStopGameAsync.
    /// </summary>
    public string? GamePackageName { get; set; }

    public void Initialize(ScriptContext context, CancellationToken token)
    {
        Context = context;
        CancellationToken = token;
    }

    /// <summary>
    /// Sets the argument values from the host (UI form or CLI).
    /// </summary>
    public void SetArguments(Dictionary<string, object> args)
    {
        Arguments = args ?? new();
    }

    /// <summary>
    /// Override to declare the bot's configuration: name, description,
    /// run mode (Standard/CustomUI/CLI), and configurable arguments.
    /// Returns null by default (Standard mode, no arguments).
    /// </summary>
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
    /// Displays an interactive menu in the console.
    /// Returns the index of the selected option.
    /// </summary>
    public virtual Task<int> ShowMenu(string title, params string[] options)
    {
        if (options == null || options.Length == 0) return Task.FromResult(-1);

        // Check if we are in a Console environment
        // Simple check: Console.WindowHeight throws if no console
        try
        {
            var _ = Console.WindowHeight;
        }
        catch
        {
            // Not a console app or no console attached
            Log("ShowMenu called but no Console available. Returning default 0.");
            return Task.FromResult(0); 
        }

        return Task.Run(() =>
        {
            int selectedIndex = 0;
            bool done = false;

            // Hide cursor
            try { Console.CursorVisible = false; } catch { }

            while (!done)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"=== {title} ===");
                Console.ResetColor();
                Console.WriteLine("Use ↑/↓ to navigate, Enter to select.\n");

                for (int i = 0; i < options.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($" > {options[i]}");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"   {options[i]}");
                    }
                }

                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex--;
                        if (selectedIndex < 0) selectedIndex = options.Length - 1;
                        break;
                    case ConsoleKey.DownArrow:
                        selectedIndex++;
                        if (selectedIndex >= options.Length) selectedIndex = 0;
                        break;
                    case ConsoleKey.Enter:
                        done = true;
                        break;
                }
            }

            try { Console.CursorVisible = true; } catch { }
            Console.Clear(); // Clear menu after selection
            
            Log($"Menu selection: {options[selectedIndex]}");
            return selectedIndex;
        });
    }

    /// <summary>
    /// Pauses the bot execution.
    /// </summary>
    public void Pause()
    {
        if (_pauseSignal == null || _pauseSignal.Task.IsCompleted)
        {
            _pauseSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            OnPausedStateChanged?.Invoke(true);
            Log("Bot paused.");
        }
    }

    /// <summary>
    /// Resumes the bot execution.
    /// </summary>
    public void Resume()
    {
        if (_pauseSignal != null)
        {
            _pauseSignal.TrySetResult(true);
            _pauseSignal = null;
            OnPausedStateChanged?.Invoke(false);
            Log("Bot resumed.");
        }
    }

    protected async Task CheckPaused()
    {
        if (_pauseSignal != null)
        {
            await _pauseSignal.Task;
        }
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

    /// <summary>
    /// Tap at coordinates that were measured at ReferenceResolution,
    /// auto-scaled to the current device resolution.
    /// Use this when hardcoding coordinates from a 1280×720 reference.
    /// </summary>
    protected async Task TapScaled(int refX, int refY)
    {
        CheckCancelled();
        var (w, h) = Context.Device.ScreenSize;
        if (w <= 0 || h <= 0)
        {
            // Fallback: try to get size from last capture
            await Context.UpdateScreenCaptureAsync(force: true);
            if (Context.LastScreenCapture != null)
            {
                w = Context.LastScreenCapture.PixelWidth;
                h = Context.LastScreenCapture.PixelHeight;
            }
        }
        var (x, y) = CoordinateScaler.Scale(refX, refY, w, h,
            ReferenceResolution.Width, ReferenceResolution.Height);
        await Context.Device.TapAsync(x, y);
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
        string? baseDir = Context.GetString("BaseDirectory");
        var template = Context.Vision.LoadTemplate(templatePath, baseDir);
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

    #region Helper Methods - Game Lifecycle

    /// <summary>
    /// Force-stop and relaunch the game. Waits for the specified delay
    /// to allow the game to fully load before returning.
    /// Requires GamePackageName to be set.
    /// </summary>
    protected async Task<bool> RestartGameAsync(int loadWaitMs = 15000)
    {
        CheckCancelled();
        if (string.IsNullOrEmpty(GamePackageName))
        {
            Log("Cannot restart game: GamePackageName not set");
            return false;
        }

        Log($"Restarting game: {GamePackageName}");

        // Step 1: Force-stop
        await Context.Device.ForceStopAppAsync(GamePackageName);
        await Delay(2000);

        // Step 2: Relaunch
        bool launched = await Context.Device.LaunchAppAsync(GamePackageName);
        if (!launched)
        {
            Log("Failed to launch game");
            return false;
        }

        // Step 3: Wait for game to load
        Log($"Game launched, waiting {loadWaitMs}ms for load...");
        await Delay(loadWaitMs);

        // Step 4: Reset health monitor
        HealthMonitor?.Reset();

        Log("Game restart complete");
        return true;
    }

    /// <summary>
    /// Force-stop the game. Useful for account switching flows.
    /// </summary>
    protected async Task<bool> ForceStopGameAsync()
    {
        CheckCancelled();
        if (string.IsNullOrEmpty(GamePackageName))
        {
            Log("Cannot stop game: GamePackageName not set");
            return false;
        }
        return await Context.Device.ForceStopAppAsync(GamePackageName);
    }

    #endregion

    #region Helper Methods - Utility

    protected async Task Delay(int ms)
    {
        CheckCancelled(); 
        await CheckPaused(); // Wait if paused

        try
        {
            await Task.Delay(ms, CancellationToken);
        }
        catch (TaskCanceledException)
        {
            throw; 
        }

        await CheckPaused(); // Wait again in case we were paused during delay? 
        // Usually checking before is enough for the next action, but checking after ensures we don't proceed if paused exactly when waking up.
    }

    public static event Action<string>? OnLogReceived;

    protected void Log(string message)
    {
        OnLogReceived?.Invoke(message);
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

    #region Helper Methods - Arguments

    /// <summary>
    /// Gets an argument value by name, with a default fallback.
    /// </summary>
    protected T GetArg<T>(string name, T defaultValue = default!)
    {
        if (Arguments.TryGetValue(name, out var value))
        {
            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[BotBase] GetArg<{typeof(T).Name}>('{name}') cast failed: {ex.Message}"); return defaultValue; }
        }
        return defaultValue;
    }

    protected string GetArgString(string name, string defaultValue = "") => GetArg(name, defaultValue);
    protected int GetArgInt(string name, int defaultValue = 0) => GetArg(name, defaultValue);
    protected bool GetArgBool(string name, bool defaultValue = false) => GetArg(name, defaultValue);
    protected double GetArgDouble(string name, double defaultValue = 0.0) => GetArg(name, defaultValue);

    #endregion
}
