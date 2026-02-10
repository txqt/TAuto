using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Service for Optical Character Recognition (OCR).
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Extract all text from the image.
    /// </summary>
    /// <param name="image">Source image.</param>
    /// <param name="language">Language code (default: eng).</param>
    /// <param name="scale">Scale factor (default: 1.0).</param>
    /// <param name="whitelist">Allowed characters (default: null).</param>
    /// <returns>Extracted text string.</returns>
    string GetText(BitmapSource image, string language = "eng", double scale = 1.0, string whitelist = null);

    /// <summary>
    /// Extract text blocks with their bounding boxes.
    /// </summary>
    /// <param name="image">Source image.</param>
    /// <param name="language">Language code (default: eng).</param>
    /// <param name="scale">Scale factor (default: 1.0).</param>
    /// <param name="whitelist">Allowed characters (default: null).</param>
    /// <returns>List of detected text blocks.</returns>
    List<OcrResultBlock> GetTextBlocks(BitmapSource image, string language = "eng", double scale = 1.0, string whitelist = null);
}

public class OcrResultBlock
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Rectangle Rect { get; set; }
    
    // Helper to get center point
    public System.Windows.Point Center => new System.Windows.Point(
        Rect.X + Rect.Width / 2, 
        Rect.Y + Rect.Height / 2);
}
