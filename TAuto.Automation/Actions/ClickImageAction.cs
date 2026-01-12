using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

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
    
    /// <summary>
    /// Optional custom name for this action.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the template image file.
    /// </summary>
    public string TemplatePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Matching threshold (0.0 - 1.0). Higher = stricter.
    /// </summary>
    public double Threshold { get; set; } = 0.8;
    
    /// <summary>
    /// Click offset X from center of found image.
    /// </summary>
    public int OffsetX { get; set; } = 0;
    
    /// <summary>
    /// Click offset Y from center of found image.
    /// </summary>
    public int OffsetY { get; set; } = 0;
    
    /// <summary>
    /// Random offset range in pixels for anti-detection.
    /// </summary>
    public int RandomOffset { get; set; } = 0;
    
    /// <summary>
    /// Timeout in milliseconds to wait for image to appear.
    /// 0 = no wait, check once.
    /// </summary>
    public int TimeoutMs { get; set; } = 0;
    
    /// <summary>
    /// Retry interval in milliseconds when waiting for image.
    /// </summary>
    // Note: ActionBase has RetryIntervalMs, but this one is specific to "Waiting for image logic" vs "Retrying the whole action".
    // We can keep it or map it. For now keeping it to avoid breaking deserialization of existing properties.
    public int RetryInterval { get; set; } = 500;
    
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
        BitmapSource? template = context.Vision.LoadTemplate(TemplatePath);
        if (template == null)
            return ActionResult.Fail($"Cannot load template: {TemplatePath}");
        
        TemplateMatchResult? matchResult = null;
        DateTime startTime = DateTime.Now;
        
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
            matchResult = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold);
            
            if (matchResult.Found)
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
        context.LastFoundImageLocation = new System.Windows.Point(tapX, tapY);
        
        // Perform tap
        bool success = await context.Device.TapAsync(tapX, tapY);
        
        return success
            ? ActionResult.Ok(matchResult.CenterLocation)
            : ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");
    }
}
