using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that taps at a specific screen location.
/// </summary>
public class TapAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    
    public override string DisplayName => !string.IsNullOrEmpty(Name) 
        ? Name 
        : UsePercent 
            ? $"Tap ({XPercent:F1}%, {YPercent:F1}%)" 
            : $"Tap ({X}, {Y})";
    
    private string _name = string.Empty;
    public string Name 
    { 
        get => _name; 
        set => SetProperty(ref _name, value); 
    }
    
    private int _x;
    public int X 
    { 
        get => _x; 
        set => SetProperty(ref _x, value); 
    }
    
    private int _y;
    public int Y 
    { 
        get => _y; 
        set => SetProperty(ref _y, value); 
    }
    
    private double _xPercent;
    public double XPercent 
    { 
        get => _xPercent; 
        set => SetProperty(ref _xPercent, value); 
    }
    
    private double _yPercent;
    public double YPercent 
    { 
        get => _yPercent; 
        set => SetProperty(ref _yPercent, value); 
    }
    
    private bool _usePercent;
    public bool UsePercent 
    { 
        get => _usePercent; 
        set => SetProperty(ref _usePercent, value); 
    }
    
    private int _randomOffset = 0;
    public int RandomOffset 
    { 
        get => _randomOffset; 
        set => SetProperty(ref _randomOffset, value); 
    }
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        if (string.IsNullOrEmpty(context.TargetId)) return ActionResult.Fail("No device connected");
        
        int tapX, tapY;
        
        if (UsePercent)
        {
            if (context.LastScreenCapture == null)
                await context.UpdateScreenCaptureAsync(force: true);
            
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot get screen dimensions");
            
            int screenWidth = context.LastScreenCapture.PixelWidth;
            int screenHeight = context.LastScreenCapture.PixelHeight;
            
            tapX = (int)(XPercent / 100.0 * screenWidth);
            tapY = (int)(YPercent / 100.0 * screenHeight);
        }
        else
        {
            tapX = X;
            tapY = Y;
        }
        
        if (RandomOffset > 0)
        {
            var rnd = new Random();
            tapX += rnd.Next(-RandomOffset, RandomOffset + 1);
            tapY += rnd.Next(-RandomOffset, RandomOffset + 1);
        }
        
        bool success = await context.Device.TapAsync(tapX, tapY);
        
        return success ? ActionResult.Ok() : ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");
    }
}
