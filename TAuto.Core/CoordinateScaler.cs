using System.Drawing;

namespace TAuto.Core;

/// <summary>
/// Utility for scaling coordinates between a reference resolution and the actual device resolution.
/// All templates and hardcoded coordinates are measured at a reference resolution (default 1280×720).
/// This scaler converts them to the actual device resolution at runtime.
/// </summary>
public static class CoordinateScaler
{
    /// <summary>
    /// Default reference resolution width. Templates are typically captured at this resolution.
    /// </summary>
    public const int DefaultRefWidth = 1280;

    /// <summary>
    /// Default reference resolution height.
    /// </summary>
    public const int DefaultRefHeight = 720;

    /// <summary>
    /// Scale absolute pixel coordinates from reference resolution to actual resolution.
    /// </summary>
    public static (int X, int Y) Scale(int x, int y,
        int actualWidth, int actualHeight,
        int refWidth = DefaultRefWidth, int refHeight = DefaultRefHeight)
    {
        double scaleX = (double)actualWidth / refWidth;
        double scaleY = (double)actualHeight / refHeight;
        return ((int)(x * scaleX), (int)(y * scaleY));
    }

    /// <summary>
    /// Scale a Point from reference resolution to actual resolution.
    /// </summary>
    public static Point Scale(Point p,
        int actualWidth, int actualHeight,
        int refWidth = DefaultRefWidth, int refHeight = DefaultRefHeight)
    {
        double scaleX = (double)actualWidth / refWidth;
        double scaleY = (double)actualHeight / refHeight;
        return new Point((int)(p.X * scaleX), (int)(p.Y * scaleY));
    }

    /// <summary>
    /// Compute the uniform scale factor between reference and actual resolution.
    /// Uses the X axis (width) as the primary scale — appropriate for 16:9 → 16:9 scaling.
    /// </summary>
    public static double GetScaleFactor(int actualWidth, int refWidth = DefaultRefWidth)
        => (double)actualWidth / refWidth;
}
