using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that finds an image template on the screen.
/// Stores the found location in ScriptContext.LastFoundImageLocation.
/// Does NOT click - use ClickImageAction for find + click.
/// </summary>
public class FindImageAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? $"🔍 {Name}"
        : $"🔍 Find Image ({Threshold:P0})";
    
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
    /// Whether to force a fresh screen capture.
    /// </summary>
    public bool ForceFreshCapture { get; set; } = true;
    
    /// <summary>
    /// Variable name to store "found" (true/false) result.
    /// If empty, result is not stored in variables.
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
        
        // Get screen capture
        await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
        if (context.LastScreenCapture == null)
            return ActionResult.Fail("Cannot capture screen");
        
        // Perform template matching
        var result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold);
        
        // Store result in context
        context.LastFoundImageLocation = result.Found ? result.CenterLocation : null;
        
        // Store in variable if configured
        if (!string.IsNullOrEmpty(ResultVariableName))
        {
            context.SetVariable(ResultVariableName, result.Found);
        }
        
        return result.Found
            ? ActionResult.Ok(result.CenterLocation)
            : ActionResult.Ok(); // Not finding is not an error, just no location stored
    }
}
