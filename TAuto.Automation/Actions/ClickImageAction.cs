using TAuto.Core;
using TAuto.Automation.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that finds an image and clicks on it.
/// Combines FindImage + Tap into a single action.
/// </summary>
[ActionMetadata("Click Image", "Vision & OCR", "🖼️")]
public class ClickImageAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? Name
        : $"Click: {TemplatePath}";
    
    private string _name = string.Empty;
    [ActionParameter("Name", "Friendly name for this action.")]
    public string Name 
    { 
        get => _name; 
        set => SetProperty(ref _name, value); 
    }
    
    private string _templatePath = string.Empty;
    [ActionParameter("Target Image", "Path to the target image file to click.", EditorType = ActionParameterEditorType.ImagePath)]
    public string TemplatePath 
    { 
        get => _templatePath; 
        set => SetProperty(ref _templatePath, value); 
    }
    
    private double _threshold = 0.8;
    [ActionParameter("Threshold", "Matching threshold (0.0 - 1.0). Higher = stricter.")]
    public double Threshold 
    { 
        get => _threshold; 
        set => SetProperty(ref _threshold, value); 
    }
    
    private int _offsetX = 0;
    [ActionParameter("Offset X", "Click offset X from center of found image.")]
    public int OffsetX 
    { 
        get => _offsetX; 
        set => SetProperty(ref _offsetX, value); 
    }
    
    private int _offsetY = 0;
    [ActionParameter("Offset Y", "Click offset Y from center of found image.")]
    public int OffsetY 
    { 
        get => _offsetY; 
        set => SetProperty(ref _offsetY, value); 
    }
    
    private int _randomOffset = 3;
    [ActionParameter("Random Offset", "Random offset range in pixels for anti-detection.")]
    public int RandomOffset 
    { 
        get => _randomOffset; 
        set => SetProperty(ref _randomOffset, value); 
    }
    
    private int _timeoutMs = 0;
    [ActionParameter("Timeout (ms)", "Timeout in ms to wait for image. 0 = check once.")]
    public int TimeoutMs 
    { 
        get => _timeoutMs; 
        set => SetProperty(ref _timeoutMs, value); 
    }
    
    private int _retryInterval = 500;
    [ActionParameter("Retry Interval", "Retry interval in ms when waiting.")]
    public int RetryInterval 
    { 
        get => _retryInterval; 
        set => SetProperty(ref _retryInterval, value); 
    }

    private int _delayAfterMs = 500;
    [ActionParameter("Delay After", "Delay in ms to wait AFTER a successful click.")]
    public int DelayAfterMs 
    { 
        get => _delayAfterMs; 
        set => SetProperty(ref _delayAfterMs, value); 
    }
    
    private int _consecutiveFrames = 1;
    [ActionParameter("Consecutive Frames", "Frames to confirm detection.")]
    public int ConsecutiveFrames 
    { 
        get => _consecutiveFrames; 
        set => SetProperty(ref _consecutiveFrames, value); 
    }
    
    private bool _forceFreshCapture = true;
    /// <summary>
    /// Whether to force a fresh screen capture for each evaluation.
    /// Default is true.
    /// </summary>
    public bool ForceFreshCapture 
    { 
        get => _forceFreshCapture; 
        set => SetProperty(ref _forceFreshCapture, value); 
    }

    private bool _disableMultiScale = true;
    /// <summary>
    /// Whether to disable multi-scale matching (searching at 70%-130% scale).
    /// Default is true for performance.
    /// </summary>
    public bool DisableMultiScale
    {
        get => _disableMultiScale;
        set => SetProperty(ref _disableMultiScale, value);
    }
    
    // ===== Execute =====
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");
            
        if (string.IsNullOrEmpty(context.TargetId))
            return ActionResult.Fail("No device connected");
        
        if (string.IsNullOrEmpty(TemplatePath))
            return ActionResult.Fail("Template path not set");
        
        // Load template
        string? baseDir = context.GetString("BaseDirectory");
        IImage? template = context.Vision.LoadTemplate(TemplatePath, baseDir);
        if (template == null)
        {
            string fullPath = string.IsNullOrEmpty(baseDir) ? TemplatePath : Path.Combine(baseDir, TemplatePath);
            return ActionResult.Fail($"Cannot load template: {TemplatePath} (Searched in: {fullPath})");
        }
        
        TemplateMatchResult? matchResult = null;
        DateTime startTime = DateTime.Now;
        var confirmation = new TAuto.Automation.Utilities.DetectionConfirmation(ConsecutiveFrames);
        
        // Wait for image with timeout
        do
        {
            if (ct.IsCancellationRequested)
                return ActionResult.Fail("Cancelled");
            
            // Capture screen
            await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot capture screen");
            
            // ✅ Try to get result from vision cache (shared across multiple actions in the same frame)
            var result = context.GetCachedMatch(TemplatePath, Threshold);

            if (result == null)
            {
                // Not in cache, perform template matching
                result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath, disableMultiScale: DisableMultiScale);
                
                // Store in cache for subsequent actions or global transitions in this frame
                context.CacheMatch(TemplatePath, Threshold, result);
            }
            
            if (result.Found)
            {
                matchResult = result;
            }
            
            if (confirmation.RecordResult(result.Found))
                break;
            
            // Check timeout
            if (TimeoutMs > 0)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                if (elapsed >= TimeoutMs)
                    return ActionResult.Fail($"Image not found after {TimeoutMs}ms timeout");
                
                // Wait before retry
                await Task.Delay(RetryInterval, ct);
            }
            
        } while (TimeoutMs > 0 && !ct.IsCancellationRequested);
        
        if (matchResult == null || !matchResult.Found)
            return ActionResult.Fail("Image not found");
        
        // Calculate click position
        int tapX = (int)matchResult.CenterLocation.X + OffsetX;
        int tapY = (int)matchResult.CenterLocation.Y + OffsetY;
        
        // Apply random offset
        if (RandomOffset > 0)
        {
            var rnd = new Random();
            tapX += rnd.Next(-RandomOffset, RandomOffset + 1);
            tapY += rnd.Next(-RandomOffset, RandomOffset + 1);
        }
        
        // Store location in context for potential subsequent actions
        context.LastFoundImageLocation = new System.Drawing.Point(tapX, tapY);
        
        // Perform tap
        bool success = await context.Device.TapAsync(tapX, tapY);
        
        if (success && DelayAfterMs > 0)
        {
            await Task.Delay(DelayAfterMs, ct);
        }
        
        return success
            ? ActionResult.Ok(matchResult.CenterLocation)
            : ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");
    }
}
