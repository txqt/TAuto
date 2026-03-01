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
    
    private bool _useScaling;
    public bool UseScaling
    {
        get => _useScaling;
        set => SetProperty(ref _useScaling, value);
    }

    private int _refWidth = CoordinateScaler.DefaultRefWidth;
    public int RefWidth
    {
        get => _refWidth;
        set => SetProperty(ref _refWidth, value);
    }

    private int _refHeight = CoordinateScaler.DefaultRefHeight;
    public int RefHeight
    {
        get => _refHeight;
        set => SetProperty(ref _refHeight, value);
    }

    private int _randomOffset = 3;
    /// <summary>
    /// Random pixel offset for anti-detection (default 3px, mandatory minimum).
    /// </summary>
    public int RandomOffset 
    { 
        get => _randomOffset; 
        set => SetProperty(ref _randomOffset, Math.Max(2, value)); 
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
            
            int screenWidth = context.LastScreenCapture.Width;
            int screenHeight = context.LastScreenCapture.Height;
            
            tapX = (int)(XPercent / 100.0 * screenWidth);
            tapY = (int)(YPercent / 100.0 * screenHeight);
        }
        else if (UseScaling)
        {
            var (w, h) = context.Device.ScreenSize;
            if (w <= 0 || h <= 0)
            {
                if (context.LastScreenCapture == null)
                    await context.UpdateScreenCaptureAsync(force: true);
                    
                if (context.LastScreenCapture != null)
                {
                    w = context.LastScreenCapture.Width;
                    h = context.LastScreenCapture.Height;
                }
                else
                {
                    return ActionResult.Fail("Cannot get screen dimensions for scaling");
                }
            }
            
            (tapX, tapY) = CoordinateScaler.Scale(X, Y, w, h, RefWidth, RefHeight);
        }
        else
        {
            tapX = X;
            tapY = Y;
        }
        
        // Apply Gaussian random offset (mandatory minimum 2px)
        int effectiveOffset = Math.Max(2, RandomOffset);
        double accMul = context.Persona?.AccuracyMultiplier ?? 1.0;
        effectiveOffset = (int)(effectiveOffset * accMul);
        {
            var rnd = new Random();
            // Box-Muller Gaussian instead of Uniform
            tapX += (int)GaussianOffset(rnd, effectiveOffset);
            tapY += (int)GaussianOffset(rnd, effectiveOffset);
        }
        
        bool success = await context.Device.TapAsync(tapX, tapY);
        
        return success ? ActionResult.Ok() : ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");
    }

    private static double GaussianOffset(Random rnd, int range)
    {
        double u1 = 1.0 - rnd.NextDouble();
        double u2 = 1.0 - rnd.NextDouble();
        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return z * (range / 2.0); // stdDev = half the range
    }
}
