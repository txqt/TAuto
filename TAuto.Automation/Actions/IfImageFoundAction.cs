using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Conditional action that checks if an image exists on screen.
/// If found, jumps to ThenActionId. If not found, jumps to ElseActionId.
/// </summary>
public class IfImageFoundAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"❓ If: {Name}"
        : $"❓ If Image Found ({Threshold:P0})";
    
    // ===== Configuration =====
    
    /// <summary>
    /// Optional custom name for this action.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the template image file to check.
    /// </summary>
    public string TemplatePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Matching threshold (0.0 - 1.0). Higher = stricter.
    /// </summary>
    public double Threshold { get; set; } = 0.8;
    
    /// <summary>
    /// Action ID to jump to if image IS found.
    /// If empty, continues to next action.
    /// </summary>
    public string ThenActionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Action ID to jump to if image is NOT found.
    /// If empty, continues to next action.
    /// </summary>
    public string ElseActionId { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to force a fresh screen capture.
    /// </summary>
    public bool ForceFreshCapture { get; set; } = true;
    
    /// <summary>
    /// If true, stores the found location in context.LastFoundImageLocation.
    /// </summary>
    public bool StoreLocation { get; set; } = true;
    
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
        
        // Get screen capture
        await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
        if (context.LastScreenCapture == null)
            return ActionResult.Fail("Cannot capture screen");
        
        // Perform template matching
        var result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold);
        
        if (result.Found)
        {
            // Store location if configured
            if (StoreLocation)
            {
                context.LastFoundImageLocation = result.CenterLocation;
            }
            
            // Jump to THEN action
            if (!string.IsNullOrEmpty(ThenActionId))
            {
                return ActionResult.Jump(ThenActionId);
            }
            return ActionResult.Ok(); // Continue sequentially
        }
        else
        {
            // Image not found - jump to ELSE action
            context.LastFoundImageLocation = null;
            
            if (!string.IsNullOrEmpty(ElseActionId))
            {
                return ActionResult.Jump(ElseActionId);
            }
            return ActionResult.Ok(); // Continue sequentially
        }
    }
}
