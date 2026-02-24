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
    
    private int _timeoutMs = 10000;
    public int TimeoutMs 
    { 
        get => _timeoutMs; 
        set => SetProperty(ref _timeoutMs, value); 
    }
    
    private int _retryInterval = 500;
    public int RetryInterval 
    { 
        get => _retryInterval; 
        set => SetProperty(ref _retryInterval, value); 
    }
    
    private bool _failOnTimeout = true;
    public bool FailOnTimeout 
    { 
        get => _failOnTimeout; 
        set => SetProperty(ref _failOnTimeout, value); 
    }
    
    private string _resultVariableName = string.Empty;
    public string ResultVariableName 
    { 
        get => _resultVariableName; 
        set => SetProperty(ref _resultVariableName, value); 
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
            var result = context.Vision.FindTemplate(context.LastScreenCapture, template, Threshold, TemplatePath);
            
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
