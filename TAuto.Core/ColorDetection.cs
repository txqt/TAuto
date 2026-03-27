using System.Drawing;
using TAuto.Core.Imaging;

namespace TAuto.Core;

public class ColorSearchOptions
{
    public Color TargetColor { get; set; }
    public int Tolerance { get; set; } = 10;
    public Rectangle? SearchRegion { get; set; }
    public int MinPixelCount { get; set; } = 1;
}

public class ColorMatchResult
{
    public bool Found { get; set; }
    public Point? CenterLocation { get; set; }
    public Rectangle? Bounds { get; set; }
    public int PixelCount { get; set; }
    public double MatchPercent { get; set; }
}

public interface IColorDetector
{
    ColorMatchResult FindColor(IImage source, ColorSearchOptions options);
}
