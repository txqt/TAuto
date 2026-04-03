using TAuto.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

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
    
    private int _consecutiveFrames = 1;
    public int ConsecutiveFrames 
    { 
        get => _consecutiveFrames; 
        set => SetProperty(ref _consecutiveFrames, value); 
    }
    
    private bool _disableMultiScale = false;
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
        
        if (string.IsNullOrEmpty(TemplatePath))
            return ActionResult.Fail("Template path not set");
        
        // Load template
        string? baseDir = context.GetString("BaseDirectory");
        IImage? template = context.Vision.LoadTemplate(TemplatePath, baseDir);
        if (template == null)
            return ActionResult.Fail($"Cannot load template: {TemplatePath}");
        
        // Perform template matching with temporal confirmation
        var confirmation = new TAuto.Automation.Utilities.DetectionConfirmation(ConsecutiveFrames);
        TemplateMatchResult? lastMatch = null;
        bool isConfirmed = false;
        
        int maxAttempts = ConsecutiveFrames == 1 ? 1 : ConsecutiveFrames + 5;
        for (int i = 0; i < maxAttempts; i++)
        {
            await context.UpdateScreenCaptureAsync(force: ForceFreshCapture);
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot capture screen");
            
            // Vision Cache (Fix Phase 4): Reuse results for the same frame
            var result = context.GetCachedMatch(TemplatePath, Threshold);
            if (result == null)
            {
                result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath, disableMultiScale: DisableMultiScale);
                context.CacheMatch(TemplatePath, Threshold, result);
            }
            if (result.Found) lastMatch = result;
            
            if (confirmation.RecordResult(result.Found))
            {
                isConfirmed = true;
                break;
            }
            
            if (ConsecutiveFrames > 1 && !isConfirmed && i < maxAttempts - 1)
            {
                await Task.Delay(100, ct);
            }
        }
        
        if (isConfirmed && lastMatch != null)
        {
            // Store location if configured
            if (StoreLocation)
            {
                context.LastFoundImageLocation = lastMatch.CenterLocation;
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
