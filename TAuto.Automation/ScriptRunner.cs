using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Actions;
using TAuto.Core;
using TAuto.Core.Models;
using TAuto.Automation.StateMachine;

namespace TAuto.Automation;

/// <summary>
/// Executes a list of actions in sequence with debugging support.
/// </summary>
public class ScriptRunner
{
    public event EventHandler<string>? OnLog;
    public event EventHandler<ActionStartedEventArgs>? OnActionStarted;
    public event EventHandler<ActionCompletedEventArgs>? OnActionCompleted;
    public event EventHandler<ScriptCompletedEventArgs>? OnScriptCompleted;
    public event EventHandler? OnPaused;
    public event EventHandler? OnResumed;
    
    private readonly ILoggerService _logger;

    public ScriptRunner(ILoggerService logger)
    {
        _logger = logger;
    }

    // Breakpoints are now properties on Actions, but we can also keep a secondary set if needed.
    // However, the requested design implies using IAction.IsBreakpoint.
    
    private volatile bool _isPaused;
    private volatile bool _shouldPauseAfterAction;
    private TaskCompletionSource<bool> _resumeSignal = new();

    public bool IsPaused => _isPaused;
    
    public ErrorPolicy ErrorPolicy { get; set; } = new();
    
    // Track execution state
    public SessionState? CurrentSession { get; private set; }
    public ScriptContext? Context { get; private set; }

    public void Pause()
    {
        _isPaused = true;
        // Re-create TCS only if it's already set (completed)
        if (_resumeSignal.Task.IsCompleted)
            _resumeSignal = new TaskCompletionSource<bool>();
            
        OnPaused?.Invoke(this, EventArgs.Empty);
    }

    public void Resume()
    {
        _isPaused = false;
        _shouldPauseAfterAction = false;
        _resumeSignal.TrySetResult(true);
        OnResumed?.Invoke(this, EventArgs.Empty);
    }

    public void Step()
    {
        // Unpause for one step, then verify logic inside loop to pause again
        _isPaused = false;
        _shouldPauseAfterAction = true;
        _resumeSignal.TrySetResult(true);
        OnResumed?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> RunAsync(List<IAction> actions, ScriptContext context, CancellationToken ct)
    {
        Context = context;
        bool success = true;
        int currentIndex = 0;
        
        // Initialize Session
        CurrentSession = new SessionState
        {
            SessionId = context.SessionId,
            ScriptPath = "Unknown", // Should be passed in or set on Context
            Variables = context.GetAllVariables(),
            StartTime = DateTime.Now
        };

        // Reset state
        _isPaused = false;
        _shouldPauseAfterAction = false;
        _resumeSignal = new TaskCompletionSource<bool>();
        _resumeSignal.SetResult(true); 
        
        // Build map for Goto by ID 
        var actionMap = new Dictionary<string, int>();
        for (int i = 0; i < actions.Count; i++)
        {
            var id = actions[i].Id;
            if (!actionMap.ContainsKey(id)) actionMap.Add(id, i);
        }

        while (currentIndex < actions.Count && !ct.IsCancellationRequested)
        {
            var action = actions[currentIndex];
            CurrentSession.CurrentIndex = currentIndex;
            
            // 1. Check Breakpoint
            if (action.IsBreakpoint)
            {
                if (!_isPaused && !_shouldPauseAfterAction)
                {
                     _isPaused = true;
                     _shouldPauseAfterAction = false;
                     _resumeSignal = new TaskCompletionSource<bool>();
                }
            }
            
            // 2. Handle Pause
            if (_isPaused)
            {
                OnPaused?.Invoke(this, EventArgs.Empty);
                try { await _resumeSignal.Task.WaitAsync(ct); }
                catch (OperationCanceledException) { break; }
                
                if (_shouldPauseAfterAction) _resumeSignal = new TaskCompletionSource<bool>();
            }

            // 3. Execute with Retry Logic
            OnActionStarted?.Invoke(this, new ActionStartedEventArgs(action, currentIndex));
            
            ActionResult result = ActionResult.Fail("Not executed");
            bool actionSuccess = false;
            int attempt = 0;
            // Determine effective retry count: Action override > Global policy
            // But usually Action settings take precedence.
            // If Action.RetryCount is 0 (default), use it. If user set it, use it.
            // We'll trust the Action property as the source of truth, assuming it defaults to 0 or was configured.
            // If we want Global defaults to apply when Action is 0, we can do that, but ActionBase defaults to 0.
            // Let's stick to Action properties for now.
            int maxRetries = action.RetryCount; 
            int retryInterval = action.RetryIntervalMs;

            while (attempt <= maxRetries && !ct.IsCancellationRequested)
            {
                if (attempt > 0)
                {
                    Log($"⚠️ Retry {attempt}/{maxRetries} for {action.DisplayName} after {retryInterval}ms...");
                    await Task.Delay(retryInterval, ct);
                }

                try
                {
                    // Special handling for State Machine to respect cancellation/pause propagation if needed in future
                    if (action is StateMachineAction sm)
                    {
                        // TODO: In Phase 2/3, we might want to pass this ScriptRunner to the StateMachine 
                        // so it can bubble up individual action events inside the state.
                        // For now, it runs as a black box.
                        result = await sm.ExecuteAsync(context, ct);
                    }
                    else
                    {
                        result = await action.ExecuteAsync(context, ct);
                    }
                    
                    if (result.Success)
                    {
                        actionSuccess = true;
                        break; // Exit retry loop
                    }
                    else
                    {
                        // Action failed logic check
                        // Some actions might fail gracefully (like CheckIf) returning simple False/Fail?
                        // Usually logic actions return OK with Jump.
                        // Fail means "Error/Exception" or "Validation Failed".
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Action Error: {ex.Message}", ex);
                    result = ActionResult.Fail($"Exception: {ex.Message}");
                }
                
                attempt++;
            }
            
            OnActionCompleted?.Invoke(this, new ActionCompletedEventArgs(action, currentIndex, result));

            // 4. Handle Result / Error
            if (!actionSuccess)
            {
                Log($"❌ Failed: {action.DisplayName} - {result.Message}");
                
                // Screenshot on Error
                if (ErrorPolicy.ScreenshotOnError)
                {
                    await CaptureErrorScreenshot(context);
                }

                if (action.ContinueOnError)
                {
                    Log("⚠️ Continuing despite error (ContinueOnError=True)");
                }
                else
                {
                    success = false;
                    break; // Stop execution
                }
            }
            else if (!string.IsNullOrEmpty(result.Message))
            {
                // Verbose success log?
            }

            // Update Session Variables
            CurrentSession.Variables = context.GetAllVariables();
            CurrentSession.LastSaveTime = DateTime.Now;
            // TODO: Trigger async persistence callback here?

            // 5. Handle Flow Control
             if (result.Data is string jumpInstruction)
            {
                 if (jumpInstruction == "SCRIPT_EXIT")
                 {
                     Log("🛑 Script stopped by condition.");
                     break; // Clean exit
                 }
                 else if (jumpInstruction.StartsWith("LOOP_EXIT:"))
                 {
                     string loopId = jumpInstruction.Replace("LOOP_EXIT:", "");
                     int endIndex = FindLoopEndIndex(actions, loopId);
                     currentIndex = (endIndex != -1) ? endIndex + 1 : currentIndex + 1;
                 }
                 else if (jumpInstruction.StartsWith("LOOP_REPEAT:"))
                 {
                     string loopId = jumpInstruction.Replace("LOOP_REPEAT:", "");
                     int startIndex = FindLoopStartIndex(actions, loopId);
                     currentIndex = (startIndex != -1) ? startIndex : currentIndex + 1;
                 }
                 else if (actionMap.TryGetValue(jumpInstruction, out int mappedIndex))
                 {
                     currentIndex = mappedIndex;
                 }
                 else
                 {
                     currentIndex++;
                 }
            }
            else
            {
                currentIndex++;
            }

            // 6. Handle Step Logic
            if (_shouldPauseAfterAction)
            {
                _isPaused = true;
                _shouldPauseAfterAction = false;
                _resumeSignal = new TaskCompletionSource<bool>();
            }
        }
        
        CurrentSession.IsCompleted = success;
        OnScriptCompleted?.Invoke(this, new ScriptCompletedEventArgs(success));
        return success;
    }
    
    private async Task CaptureErrorScreenshot(ScriptContext context)
    {
        try 
        {
            if (!string.IsNullOrEmpty(ErrorPolicy.ScreenshotDirectory))
            {
                string filename = $"Error_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string path = System.IO.Path.Combine(ErrorPolicy.ScreenshotDirectory, filename);
                System.IO.Directory.CreateDirectory(ErrorPolicy.ScreenshotDirectory);
                
                 await context.UpdateScreenCaptureAsync(force: false); // Use last if available
                 if (context.LastScreenCapture != null)
                 {
                     // Save logic - BitmapSource to File
                     var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                     encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(context.LastScreenCapture));

                     using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
                     {
                         encoder.Save(stream);
                     }
                     
                     Log($"📸 Saved Error Screenshot to {path}");
                 }
            }
        }
        catch (Exception ex) { Log($"Failed to capture error screenshot: {ex.Message}"); }
    }

    private int FindLoopEndIndex(List<IAction> actions, string loopId)
    {
        return actions.FindIndex(a => a is LoopEndAction end && end.LoopId == loopId);
    }
    
    private int FindLoopStartIndex(List<IAction> actions, string loopId)
    {
        return actions.FindIndex(a => (a is LoopStartAction start && start.LoopId == loopId) 
                                   || (a is WhileAction w && w.LoopId == loopId));
    }

    private void Log(string message) 
    {
        _logger.Info(message);
        OnLog?.Invoke(this, message);
    }
}

public class ActionStartedEventArgs : EventArgs
{
    public IAction Action { get; }
    public int ActionIndex { get; }
    public ActionStartedEventArgs(IAction action, int index) { Action = action; ActionIndex = index; }
}

public class ActionCompletedEventArgs : EventArgs
{
    public IAction Action { get; }
    public int ActionIndex { get; }
    public ActionResult Result { get; }
    public ActionCompletedEventArgs(IAction action, int index, ActionResult result) 
    { Action = action; ActionIndex = index; Result = result; }
}

public class ScriptCompletedEventArgs : EventArgs
{
    public bool Success { get; }
    public ScriptCompletedEventArgs(bool success) { Success = success; }
}
