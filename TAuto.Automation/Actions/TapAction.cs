using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that taps at a specific screen location.
/// </summary>
[ActionMetadata("Tap", "Input", "T")]
public class TapAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? Name
        : UsePercent
            ? $"Tap ({XPercent:F1}%, {YPercent:F1}%)"
            : $"Tap ({X}, {Y})";

    private string _name = string.Empty;

    [ActionParameter("Name", "Optional friendly name shown in the editor.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private int _x;

    [ActionParameter("X", "Absolute X coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateX)]
    public int X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    private int _y;

    [ActionParameter("Y", "Absolute Y coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateY)]
    public int Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    private double _xPercent;

    [ActionParameter("X (%)", "Relative X coordinate as a percentage.", IsAdvanced = true)]
    public double XPercent
    {
        get => _xPercent;
        set => SetProperty(ref _xPercent, value);
    }

    private double _yPercent;

    [ActionParameter("Y (%)", "Relative Y coordinate as a percentage.", IsAdvanced = true)]
    public double YPercent
    {
        get => _yPercent;
        set => SetProperty(ref _yPercent, value);
    }

    private bool _usePercent;

    [ActionParameter("Use Percent", "Use percentage-based coordinates instead of absolute pixels.", Group = "Coordinate Mode")]
    public bool UsePercent
    {
        get => _usePercent;
        set => SetProperty(ref _usePercent, value);
    }

    private bool _useScaling;

    [ActionParameter("Use Scaling", "Scale absolute coordinates from a reference resolution.", Group = "Coordinate Mode", IsAdvanced = true)]
    public bool UseScaling
    {
        get => _useScaling;
        set => SetProperty(ref _useScaling, value);
    }

    private int _refWidth = CoordinateScaler.DefaultRefWidth;

    [ActionParameter("Reference Width", "Original width used when recording coordinates.", Group = "Coordinate Mode", IsAdvanced = true)]
    public int RefWidth
    {
        get => _refWidth;
        set => SetProperty(ref _refWidth, value);
    }

    private int _refHeight = CoordinateScaler.DefaultRefHeight;

    [ActionParameter("Reference Height", "Original height used when recording coordinates.", Group = "Coordinate Mode", IsAdvanced = true)]
    public int RefHeight
    {
        get => _refHeight;
        set => SetProperty(ref _refHeight, value);
    }

    private int _randomOffset = 3;

    [ActionParameter("Random Offset", "Adds small randomness for more human-like taps.", IsAdvanced = true)]
    public int RandomOffset
    {
        get => _randomOffset;
        set => SetProperty(ref _randomOffset, Math.Max(2, value));
    }

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return ActionResult.Fail("Cancelled");
        }

        if (string.IsNullOrEmpty(context.TargetId))
        {
            return ActionResult.Fail("No device connected");
        }

        int tapX;
        int tapY;

        if (UsePercent)
        {
            if (context.LastScreenCapture == null)
            {
                await context.UpdateScreenCaptureAsync(force: true);
            }

            if (context.LastScreenCapture == null)
            {
                return ActionResult.Fail("Cannot get screen dimensions");
            }

            tapX = (int)(XPercent / 100.0 * context.LastScreenCapture.Width);
            tapY = (int)(YPercent / 100.0 * context.LastScreenCapture.Height);
        }
        else if (UseScaling)
        {
            var (width, height) = context.Device.ScreenSize;
            if (width <= 0 || height <= 0)
            {
                if (context.LastScreenCapture == null)
                {
                    await context.UpdateScreenCaptureAsync(force: true);
                }

                if (context.LastScreenCapture == null)
                {
                    return ActionResult.Fail("Cannot get screen dimensions for scaling");
                }

                width = context.LastScreenCapture.Width;
                height = context.LastScreenCapture.Height;
            }

            (tapX, tapY) = CoordinateScaler.Scale(X, Y, width, height, RefWidth, RefHeight);
        }
        else
        {
            tapX = X;
            tapY = Y;
        }

        var effectiveOffset = Math.Max(2, RandomOffset);
        var accMul = context.Persona?.AccuracyMultiplier ?? 1.0;
        effectiveOffset = (int)(effectiveOffset * accMul);
        var rnd = new Random();
        tapX += (int)GaussianOffset(rnd, effectiveOffset);
        tapY += (int)GaussianOffset(rnd, effectiveOffset);

        var success = await context.Device.TapAsync(tapX, tapY);
        return success ? ActionResult.Ok() : ActionResult.Fail($"Tap failed at ({tapX}, {tapY})");
    }

    private static double GaussianOffset(Random rnd, int range)
    {
        var u1 = 1.0 - rnd.NextDouble();
        var u2 = 1.0 - rnd.NextDouble();
        var z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        return z * (range / 2.0);
    }
}
