using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Automation.Models;
using TAuto.Core;

namespace TAuto.Automation.Actions;

/// <summary>
/// Action that swipes from one point to another.
/// </summary>
[ActionMetadata("Swipe", "Input", "S")]
public class SwipeAction : ActionBase
{
    public override string DisplayName => !string.IsNullOrEmpty(Name)
        ? Name
        : UsePercent
            ? $"Swipe ({StartXPercent:F0}%,{StartYPercent:F0}%) -> ({EndXPercent:F0}%,{EndYPercent:F0}%)"
            : $"Swipe ({StartX},{StartY}) -> ({EndX},{EndY})";

    private string _name = string.Empty;

    [ActionParameter("Name", "Optional friendly name shown in the editor.")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private int _startX;

    [ActionParameter("Start X", "Swipe start X coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateX)]
    public int StartX
    {
        get => _startX;
        set => SetProperty(ref _startX, value);
    }

    private int _startY;

    [ActionParameter("Start Y", "Swipe start Y coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateY)]
    public int StartY
    {
        get => _startY;
        set => SetProperty(ref _startY, value);
    }

    private int _endX;

    [ActionParameter("End X", "Swipe end X coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateX)]
    public int EndX
    {
        get => _endX;
        set => SetProperty(ref _endX, value);
    }

    private int _endY;

    [ActionParameter("End Y", "Swipe end Y coordinate in pixels.", EditorType = ActionParameterEditorType.CoordinateY)]
    public int EndY
    {
        get => _endY;
        set => SetProperty(ref _endY, value);
    }

    private double _startXPercent;

    [ActionParameter("Start X (%)", "Swipe start X as a percentage.", IsAdvanced = true)]
    public double StartXPercent
    {
        get => _startXPercent;
        set => SetProperty(ref _startXPercent, value);
    }

    private double _startYPercent;

    [ActionParameter("Start Y (%)", "Swipe start Y as a percentage.", IsAdvanced = true)]
    public double StartYPercent
    {
        get => _startYPercent;
        set => SetProperty(ref _startYPercent, value);
    }

    private double _endXPercent;

    [ActionParameter("End X (%)", "Swipe end X as a percentage.", IsAdvanced = true)]
    public double EndXPercent
    {
        get => _endXPercent;
        set => SetProperty(ref _endXPercent, value);
    }

    private double _endYPercent;

    [ActionParameter("End Y (%)", "Swipe end Y as a percentage.", IsAdvanced = true)]
    public double EndYPercent
    {
        get => _endYPercent;
        set => SetProperty(ref _endYPercent, value);
    }

    private bool _usePercent;

    [ActionParameter("Use Percent", "Use percentage-based coordinates instead of pixels.", Group = "Coordinate Mode")]
    public bool UsePercent
    {
        get => _usePercent;
        set => SetProperty(ref _usePercent, value);
    }

    private int _durationMs = 300;

    [ActionParameter("Duration (ms)", "Swipe duration in milliseconds.")]
    public int DurationMs
    {
        get => _durationMs;
        set => SetProperty(ref _durationMs, value);
    }

    private int _randomOffset = 3;

    [ActionParameter("Random Offset", "Adds small randomness at swipe endpoints.", IsAdvanced = true)]
    public int RandomOffset
    {
        get => _randomOffset;
        set => SetProperty(ref _randomOffset, value);
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

        int x1;
        int y1;
        int x2;
        int y2;

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

            x1 = (int)(StartXPercent / 100.0 * context.LastScreenCapture.Width);
            y1 = (int)(StartYPercent / 100.0 * context.LastScreenCapture.Height);
            x2 = (int)(EndXPercent / 100.0 * context.LastScreenCapture.Width);
            y2 = (int)(EndYPercent / 100.0 * context.LastScreenCapture.Height);
        }
        else
        {
            x1 = StartX;
            y1 = StartY;
            x2 = EndX;
            y2 = EndY;
        }

        if (RandomOffset > 0)
        {
            var rnd = new Random();
            x1 += rnd.Next(-RandomOffset, RandomOffset + 1);
            y1 += rnd.Next(-RandomOffset, RandomOffset + 1);
            x2 += rnd.Next(-RandomOffset, RandomOffset + 1);
            y2 += rnd.Next(-RandomOffset, RandomOffset + 1);
        }

        var success = await context.Device.SwipeAsync(x1, y1, x2, y2, DurationMs);
        return success ? ActionResult.Ok() : ActionResult.Fail($"Swipe failed from ({x1},{y1}) to ({x2},{y2})");
    }
}
