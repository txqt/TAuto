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
    /// <returns>Extracted text string.</returns>
    string GetText(BitmapSource image, string language = "eng");

    /// <summary>
    /// Extract text blocks with their bounding boxes.
    /// </summary>
    /// <param name="image">Source image.</param>
    /// <param name="language">Language code (default: eng).</param>
    /// <returns>List of detected text blocks.</returns>
    List<OcrResultBlock> GetTextBlocks(BitmapSource image, string language = "eng");
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
