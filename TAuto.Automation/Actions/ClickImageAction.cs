using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that finds an image and clicks on it.
/// Combines FindImage + Tap into a single action.
/// </summary>
public class ClickImageAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => $"👆 Click {Name}";
    
    // ===== Configuration =====
    
    private string _name = string.Empty;
    /// <summary>
    /// Optional custom name for this action.
    /// </summary>
    public string Name 
    { 
        get => _name; 
        set => SetProperty(ref _name, value); 
    }
    
    private string _templatePath = string.Empty;
    /// <summary>
    /// Path to the template image file.
    /// </summary>
    public string TemplatePath 
    { 
        get => _templatePath; 
        set => SetProperty(ref _templatePath, value); 
    }
    
    private double _threshold = 0.8;
    /// <summary>
    /// Matching threshold (0.0 - 1.0). Higher = stricter.
    /// </summary>
    public double Threshold 
    { 
        get => _threshold; 
        set => SetProperty(ref _threshold, value); 
    }
    
    private int _offsetX = 0;
    /// <summary>
    /// Click offset X from center of found image.
    /// </summary>
    public int OffsetX 
    { 
        get => _offsetX; 
        set => SetProperty(ref _offsetX, value); 
    }
    
    private int _offsetY = 0;
    /// <summary>
    /// Click offset Y from center of found image.
    /// </summary>
    public int OffsetY 
    { 
        get => _offsetY; 
        set => SetProperty(ref _offsetY, value); 
    }
    
    private int _randomOffset = 3;
    /// <summary>
    /// Random offset range in pixels for anti-detection.
    /// </summary>
    public int RandomOffset 
    { 
        get => _randomOffset; 
        set => SetProperty(ref _randomOffset, value); 
    }
    
    private int _timeoutMs = 0;
    /// <summary>
    /// Timeout in milliseconds to wait for image to appear.
    /// 0 = no wait, check once.
    /// </summary>
    public int TimeoutMs 
    { 
        get => _timeoutMs; 
        set => SetProperty(ref _timeoutMs, value); 
    }
    
    private int _retryInterval = 500;
    /// <summary>
    /// Retry interval in milliseconds when waiting for image.
    /// </summary>
    public int RetryInterval 
    { 
        get => _retryInterval; 
        set => SetProperty(ref _retryInterval, value); 
    }

    private int _delayAfterMs = 500;
    /// <summary>
    /// Delay in milliseconds to wait AFTER a successful click.
    /// Useful for letting UI animations finish.
    /// </summary>
    public int DelayAfterMs 
    { 
        get => _delayAfterMs; 
        set => SetProperty(ref _delayAfterMs, value); 
    }
    
    private int _consecutiveFrames = 1;
    /// <summary>
    /// Number of consecutive frames the image must be detected before acting.
    /// Prevents false positives from single-frame noise. Default 1.
    /// </summary>
    public int ConsecutiveFrames 
    { 
        get => _consecutiveFrames; 
        set => SetProperty(ref _consecutiveFrames, value); 
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
            return ActionResult.Fail($"Cannot load template: {TemplatePath}");
        
        TemplateMatchResult? matchResult = null;
        DateTime startTime = DateTime.Now;
        var confirmation = new TAuto.Automation.Utilities.DetectionConfirmation(ConsecutiveFrames);
        
        // Wait for image with timeout
        do
        {
            if (ct.IsCancellationRequested)
                return ActionResult.Fail("Cancelled");
            
            // Capture screen
            await context.UpdateScreenCaptureAsync(force: true);
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot capture screen");
            
            // Try to find image
            var result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath);
            
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
