using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that waits until a specific image appears on screen.
/// Does NOT click - just waits and stores location in context.
/// </summary>
public class WaitForImageAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"⏳ Wait: {Name}"
        : $"⏳ Wait for Image ({TimeoutMs}ms)";
    
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
    /// Timeout in milliseconds to wait for image.
    /// </summary>
    public int TimeoutMs { get; set; } = 10000;
    
    // Note: ActionBase has RetryIntervalMs, but this is for finding logic.
    // Keeping this prop to be explicit or we can map it.
    public int RetryInterval { get; set; } = 500;
    
    /// <summary>
    /// If true, action fails when timeout is reached.
    /// If false, action succeeds but stores false in result variable.
    /// </summary>
    public bool FailOnTimeout { get; set; } = true;
    
    /// <summary>
    /// Variable name to store "found" (true/false) result.
    /// </summary>
    public string ResultVariableName { get; set; } = string.Empty;
    
    // ===== Execute =====
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");
        
        if (string.IsNullOrEmpty(TemplatePath))
            return ActionResult.Fail("Template path not set");
        
        // Load template
        BitmapSource? template = context.Vision.LoadTemplate(TemplatePath);
        if (template == null)
            return ActionResult.Fail($"Cannot load template: {TemplatePath}");
        
        DateTime startTime = DateTime.Now;
        
        while (!ct.IsCancellationRequested)
        {
            // Capture screen
            await context.UpdateScreenCaptureAsync(force: true);
            if (context.LastScreenCapture == null)
            {
                await Task.Delay(RetryInterval, ct);
                continue;
            }
            
            // Try to find image
            var result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold);
            
            if (result.Found)
            {
                context.LastFoundImageLocation = result.CenterLocation;
                
                if (!string.IsNullOrEmpty(ResultVariableName))
                    context.SetVariable(ResultVariableName, true);
                
                return ActionResult.Ok(result.CenterLocation);
            }
            
            // Check timeout
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            if (elapsed >= TimeoutMs)
            {
                if (!string.IsNullOrEmpty(ResultVariableName))
                    context.SetVariable(ResultVariableName, false);
                
                if (FailOnTimeout)
                    return ActionResult.Fail($"Image not found after {TimeoutMs}ms");
                else
                    return ActionResult.Ok(); // Continue without error
            }
            
            await Task.Delay(RetryInterval, ct);
        }
        
        return ActionResult.Fail("Cancelled");
    }
}
