using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;
using TAuto.Automation.Models;

namespace TAuto.Automation.Actions;

public class LongPressAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? Name
        : UsePercent
            ? $"Long Press ({XPercent:F1}%, {YPercent:F1}%)"
            : $"Long Press ({X}, {Y})";

    private string _name = string.Empty;

    [ActionParameter("Name", "Optional friendly name shown in the editor.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private int _x;
    [ActionParameter("X", "X coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateX)]
    public int X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    private int _y;
    [ActionParameter("Y", "Y coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateY)]
    public int Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }

    private double _xPercent;
    [ActionParameter("X (%)", "X coordinate as a percentage.", IsAdvanced = true)]
    public double XPercent
    {
        get => _xPercent;
        set => SetProperty(ref _xPercent, value);
    }

    private double _yPercent;
    [ActionParameter("Y (%)", "Y coordinate as a percentage.", IsAdvanced = true)]
    public double YPercent
    {
        get => _yPercent;
        set => SetProperty(ref _yPercent, value);
    }

    private bool _usePercent;
    [ActionParameter("Use Percent", "Use percentage-based coordinates instead of pixels.", Group = "Coordinate Mode")]
    public bool UsePercent
    {
        get => _usePercent;
        set => SetProperty(ref _usePercent, value);
    }

    private int _durationMs = 500;
    [ActionParameter("Duration (ms)", "Long press duration in milliseconds.")]
    public int DurationMs
    {
        get => _durationMs;
        set => SetProperty(ref _durationMs, value);
    }

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return ActionResult.Fail("Cancelled");
        if (string.IsNullOrEmpty(context.TargetId)) return ActionResult.Fail("No device connected");

        int pressX;
        int pressY;

        if (UsePercent)
        {
            if (context.LastScreenCapture == null)
                await context.UpdateScreenCaptureAsync(force: true);

            if (context.LastScreenCapture == null)
                return ActionResult.Fail("Cannot get screen dimensions");

            int screenWidth = context.LastScreenCapture.Width;
            int screenHeight = context.LastScreenCapture.Height;

            pressX = (int)(XPercent / 100.0 * screenWidth);
            pressY = (int)(YPercent / 100.0 * screenHeight);
        }
        else
        {
            pressX = X;
            pressY = Y;
        }

        bool success = await context.Device.LongPressAsync(pressX, pressY, DurationMs);

        return success ? ActionResult.Ok() : ActionResult.Fail($"Long press failed at ({pressX}, {pressY})");
    }
}
