using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// AUDIT FIX (P0-5): Full game restart recovery action.
/// </summary>
[ActionMetadata("Restart Game", "System", "🔄", IsAdvanced = true)]
public class RestartGameAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"🔄 Restart: {Name}"
        : "🔄 Restart Game";

    // ===== Configuration =====

    private string _name = string.Empty;
    [ActionParameter("Name", "Friendly name for this action.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _processName = string.Empty;
    [ActionParameter("Process Name", "Process name to kill (e.g., game).")]
    public string ProcessName
    {
        get => _processName;
        set => SetProperty(ref _processName, value);
    }

    private string _exePath = string.Empty;
    [ActionParameter("Exe Path", "Full path to executable for relaunch.")]
    public string ExePath
    {
        get => _exePath;
        set => SetProperty(ref _exePath, value);
    }

    /// <summary>
    /// Optional launch arguments for the executable.
    /// Falls back to context variable "AppConfig.LaunchArgs" if empty.
    /// </summary>
    private string _launchArgs = string.Empty;
    public string LaunchArgs
    {
        get => _launchArgs;
        set => SetProperty(ref _launchArgs, value);
    }

    /// <summary>
    /// Window title to search for after relaunch.
    /// Falls back to context variable "AppConfig.WindowTitle" if empty.
    /// </summary>
    private string _windowTitle = string.Empty;
    public string WindowTitle
    {
        get => _windowTitle;
        set => SetProperty(ref _windowTitle, value);
    }

    /// <summary>Cooldown after killing the process before relaunching (ms). Default: 5000.</summary>
    private int _cooldownMs = 5000;
    public int CooldownMs
    {
        get => _cooldownMs;
        set => SetProperty(ref _cooldownMs, value);
    }

    /// <summary>Max time to wait for the game window to appear after relaunch (ms). Default: 30000.</summary>
    private int _windowReadyTimeoutMs = 30000;
    public int WindowReadyTimeoutMs
    {
        get => _windowReadyTimeoutMs;
        set => SetProperty(ref _windowReadyTimeoutMs, value);
    }

    /// <summary>
    /// Optional: template image path to confirm the game is on the expected screen (e.g., main menu).
    /// If empty, the action succeeds as soon as the window handle is found.
    /// </summary>
    private string _readyTemplatePath = string.Empty;
    public string ReadyTemplatePath
    {
        get => _readyTemplatePath;
        set => SetProperty(ref _readyTemplatePath, value);
    }

    /// <summary>Match threshold for the ready template. Default: 0.8.</summary>
    private double _readyThreshold = 0.8;
    public double ReadyThreshold
    {
        get => _readyThreshold;
        set => SetProperty(ref _readyThreshold, value);
    }

    /// <summary>Max time to wait for the ready template after window appears (ms). Default: 60000.</summary>
    private int _readyTimeoutMs = 60000;
    public int ReadyTimeoutMs
    {
        get => _readyTimeoutMs;
        set => SetProperty(ref _readyTimeoutMs, value);
    }

    // ===== Execute =====

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");

        // Resolve configuration: action properties override context variables
        string processName = ResolveConfig(ProcessName, context, "AppConfig.ProcessName");
        string exePath = ResolveConfig(ExePath, context, "AppConfig.ExePath");
        string launchArgs = ResolveConfig(LaunchArgs, context, "AppConfig.LaunchArgs");
        string windowTitle = ResolveConfig(WindowTitle, context, "AppConfig.WindowTitle");

        if (string.IsNullOrEmpty(processName) && string.IsNullOrEmpty(exePath))
            return ActionResult.Fail("RestartGame: Neither ProcessName nor ExePath configured");

        context.Logger?.Info($"[RestartGame] Starting game restart sequence. Process={processName}, Exe={exePath}");

        // ── Phase 1: Kill existing process ──
        if (!string.IsNullOrEmpty(processName))
        {
            try
            {
                var processes = Process.GetProcessesByName(
                    processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? processName[..^4]
                        : processName);

                foreach (var proc in processes)
                {
                    try
                    {
                        context.Logger?.Info($"[RestartGame] Killing process {proc.ProcessName} (PID: {proc.Id})");
                        proc.Kill();
                        // Wait up to 10 seconds for the process to actually exit
                        if (!proc.WaitForExit(10000))
                        {
                            context.Logger?.Warning($"[RestartGame] Process {proc.Id} did not exit within 10s after Kill()");
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Logger?.Warning($"[RestartGame] Failed to kill PID {proc.Id}: {ex.Message}");
                    }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                context.Logger?.Warning($"[RestartGame] Error during process kill phase: {ex.Message}");
            }
        }

        // ── Phase 2: Cooldown ──
        context.Logger?.Info($"[RestartGame] Cooling down for {CooldownMs}ms...");
        await Task.Delay(CooldownMs, ct);

        // ── Phase 3: Relaunch ──
        if (!string.IsNullOrEmpty(exePath))
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = launchArgs ?? "",
                    UseShellExecute = true
                };

                var newProcess = Process.Start(startInfo);
                if (newProcess == null)
                    return ActionResult.Fail("RestartGame: Process.Start returned null");

                context.Logger?.Info($"[RestartGame] Launched {exePath} (PID: {newProcess.Id})");
                newProcess.Dispose(); // We don't need to track the process handle
            }
            catch (Exception ex)
            {
                return ActionResult.Fail($"RestartGame: Failed to launch: {ex.Message}");
            }
        }
        else
        {
            context.Logger?.Info("[RestartGame] No ExePath configured, skipping relaunch (process may self-restart)");
        }

        // ── Phase 4: Wait for window handle ──
        if (!string.IsNullOrEmpty(windowTitle))
        {
            context.Logger?.Info($"[RestartGame] Waiting for window '{windowTitle}' (timeout: {WindowReadyTimeoutMs}ms)...");

            var windowStart = DateTime.UtcNow;
            IntPtr hwnd = IntPtr.Zero;

            while ((DateTime.UtcNow - windowStart).TotalMilliseconds < WindowReadyTimeoutMs)
            {
                ct.ThrowIfCancellationRequested();

                // Search for the window by title
                hwnd = FindWindowByTitle(windowTitle);
                if (hwnd != IntPtr.Zero)
                {
                    context.Logger?.Info($"[RestartGame] Window found: hwnd=0x{hwnd:X}");
                    break;
                }

                await Task.Delay(1000, ct); // Poll every 1 second
            }

            if (hwnd == IntPtr.Zero)
                return ActionResult.Fail($"RestartGame: Window '{windowTitle}' not found within {WindowReadyTimeoutMs}ms");

            // Update the device controller target
            context.Device.TargetId = hwnd.ToString();
            context.Logger?.Info($"[RestartGame] Device target updated to hwnd=0x{hwnd:X}");
        }

        // ── Phase 5: Wait for UI readiness (optional template match) ──
        if (!string.IsNullOrEmpty(ReadyTemplatePath))
        {
            context.Logger?.Info($"[RestartGame] Waiting for ready template '{ReadyTemplatePath}' (timeout: {ReadyTimeoutMs}ms)...");

            string? baseDir = context.GetString("BaseDirectory");
            IImage? readyTemplate = context.Vision.LoadTemplate(ReadyTemplatePath, baseDir);
            if (readyTemplate == null)
            {
                context.Logger?.Warning($"[RestartGame] Could not load ready template: {ReadyTemplatePath}, proceeding anyway");
            }
            else
            {
                var uiStart = DateTime.UtcNow;
                bool uiReady = false;

                while ((DateTime.UtcNow - uiStart).TotalMilliseconds < ReadyTimeoutMs)
                {
                    ct.ThrowIfCancellationRequested();

                    await context.UpdateScreenCaptureAsync(force: true);
                    if (context.LastScreenCapture != null)
                    {
                        var match = context.Vision.FindTemplate(
                            context.LastScreenCapture, readyTemplate, ReadyThreshold, ReadyTemplatePath);

                        if (match.Found)
                        {
                            context.Logger?.Info($"[RestartGame] Ready template matched (confidence: {match.Confidence:P0})");
                            uiReady = true;
                            break;
                        }
                    }

                    await Task.Delay(2000, ct); // Poll every 2 seconds — game loading is slow
                }

                if (!uiReady)
                {
                    return ActionResult.Fail($"RestartGame: UI ready template not found within {ReadyTimeoutMs}ms");
                }
            }
        }

        // ── Phase 6: Reset health monitor and signal success ──
        context.HealthMonitor?.Reset();
        context.Logger?.Info("[RestartGame] Game restart sequence completed successfully");

        return ActionResult.Ok();
    }

    // ===== Helpers =====

    private static string ResolveConfig(string actionValue, ScriptContext context, string contextKey)
    {
        if (!string.IsNullOrEmpty(actionValue))
            return actionValue;
        return context.GetString(contextKey, "");
    }

    /// <summary>
    /// Find a window by its title using Process enumeration.
    /// This avoids P/Invoke in the core TAuto.Automation project.
    /// </summary>
    private static IntPtr FindWindowByTitle(string title)
    {
        try
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                try
                {
                    if (!string.IsNullOrEmpty(proc.MainWindowTitle) &&
                        proc.MainWindowTitle.Contains(title, StringComparison.OrdinalIgnoreCase) &&
                        proc.MainWindowHandle != IntPtr.Zero)
                    {
                        return proc.MainWindowHandle;
                    }
                }
                catch
                {
                    // Some processes throw on property access — ignore
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch
        {
            // Ignore enumeration failures
        }

        return IntPtr.Zero;
    }
}
