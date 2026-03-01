using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that swipes from one point to another.
/// </summary>
public class SwipeAction : ActionBase
{
    // Id and IsBreakpoint are in ActionBase
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? Name
        : UsePercent
            ? $"Swipe ({StartXPercent:F0}%,{StartYPercent:F0}%) → ({EndXPercent:F0}%,{EndYPercent:F0}%)"
            : $"Swipe ({StartX},{StartY}) → ({EndX},{EndY})";
    
    private string _name = string.Empty;
    public string Name 
    { 
        get => _name; 
        set => SetProperty(ref _name, value); 
    }
    
    private int _startX;
    public int StartX 
    { 
        get => _startX; 
        set => SetProperty(ref _startX, value); 
    }
    
    private int _startY;
    public int StartY 
    { 
        get => _startY; 
        set => SetProperty(ref _startY, value); 
    }
    
    private int _endX;
    public int EndX 
    { 
        get => _endX; 
        set => SetProperty(ref _endX, value); 
    }
    
    private int _endY;
    public int EndY 
    { 
        get => _endY; 
        set => SetProperty(ref _endY, value); 
    }
    
    private double _startXPercent;
    public double StartXPercent 
    { 
        get => _startXPercent; 
        set => SetProperty(ref _startXPercent, value); 
    }
    
    private double _startYPercent;
    public double StartYPercent 
    { 
        get => _startYPercent; 
        set => SetProperty(ref _startYPercent, value); 
    }
    
    private double _endXPercent;
    public double EndXPercent 
    { 
        get => _endXPercent; 
        set => SetProperty(ref _endXPercent, value); 
    }
    
    private double _endYPercent;
    public double EndYPercent 
    { 
        get => _endYPercent; 
        set => SetProperty(ref _endYPercent, value); 
    }
    
    private bool _usePercent;
    public bool UsePercent 
    { 
        get => _usePercent; 
        set => SetProperty(ref _usePercent, value); 
    }
    
    private int _durationMs = 300;
    public int DurationMs 
    { 
        get => _durationMs; 
        set => SetProperty(ref _durationMs, value); 
    }
    
    private int _randomOffset = 3;
    public int RandomOffset 
    { 
        get => _randomOffset; 
        set => SetProperty(ref _randomOffset, value); 
    }
    
    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        if (string.IsNullOrEmpty(context.TargetId)) return ActionResult.Fail("No device connected");
        
        int x1, y1, x2, y2;
        
        if (UsePercent)
        {
            if (context.LastScreenCapture == null)
                await context.UpdateScreenCaptureAsync(force: true);
            
            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot get screen dimensions");
            
            int screenWidth = context.LastScreenCapture.Width;
            int screenHeight = context.LastScreenCapture.Height;
            
            x1 = (int)(StartXPercent / 100.0 * screenWidth);
            y1 = (int)(StartYPercent / 100.0 * screenHeight);
            x2 = (int)(EndXPercent / 100.0 * screenWidth);
            y2 = (int)(EndYPercent / 100.0 * screenHeight);
        }
        else
        {
            x1 = StartX; y1 = StartY; x2 = EndX; y2 = EndY;
        }
        
        if (RandomOffset > 0)
        {
            var rnd = new Random();
            x1 += rnd.Next(-RandomOffset, RandomOffset + 1);
            y1 += rnd.Next(-RandomOffset, RandomOffset + 1);
            x2 += rnd.Next(-RandomOffset, RandomOffset + 1);
            y2 += rnd.Next(-RandomOffset, RandomOffset + 1);
        }
        
        bool success = await context.Device.SwipeAsync(x1, y1, x2, y2, DurationMs);
        
        return success ? ActionResult.Ok() : ActionResult.Fail($"Swipe failed from ({x1},{y1}) to ({x2},{y2})");
    }
}
