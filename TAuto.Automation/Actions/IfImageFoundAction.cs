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
    
    private string _name = string.Empty;
    public string Name 
    { 
        get => _name; 
        set => SetProperty(ref _name, value); 
    }
    
    private string _templatePath = string.Empty;
    public string TemplatePath 
    { 
        get => _templatePath; 
        set => SetProperty(ref _templatePath, value); 
    }
    
    private double _threshold = 0.8;
    public double Threshold 
    { 
        get => _threshold; 
        set => SetProperty(ref _threshold, value); 
    }
    
    private string _thenActionId = string.Empty;
    public string ThenActionId 
    { 
        get => _thenActionId; 
        set => SetProperty(ref _thenActionId, value); 
    }
    
    private string _elseActionId = string.Empty;
    public string ElseActionId 
    { 
        get => _elseActionId; 
        set => SetProperty(ref _elseActionId, value); 
    }
    
    private bool _forceFreshCapture = true;
    public bool ForceFreshCapture 
    { 
        get => _forceFreshCapture; 
        set => SetProperty(ref _forceFreshCapture, value); 
    }
    
    private bool _storeLocation = true;
    public bool StoreLocation 
    { 
        get => _storeLocation; 
        set => SetProperty(ref _storeLocation, value); 
    }
    
    // ===== Execute =====
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return ActionResult.Fail("Cancelled");
        
        if (string.IsNullOrEmpty(TemplatePath))
            return ActionResult.Fail("Template path not set");
        
        // Load template
        string? baseDir = context.GetString("BaseDirectory");
        BitmapSource? template = context.Vision.LoadTemplate(TemplatePath, baseDir);
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
            return ActionResult.Fail("Image not found"); // False for StateMachine
        }
    }
}
