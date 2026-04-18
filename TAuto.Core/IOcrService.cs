using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using TAuto.Core.Imaging;

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
    /// <param name="threshold">Binary threshold (0=disabled).</param>
    /// <param name="invert">Invert colors before threshold.</param>
    /// <param name="borderSize">Border pixels around text.</param>
    /// <param name="pageSegMode">Tesseract PSM: 3=auto, 7=single line, 8=single word, 13=raw line.</param>
    /// <returns>Extracted text string.</returns>
    string GetText(IImage image, string language = "eng", double scale = 1.0, string whitelist = null, int threshold = 0, bool invert = false, int borderSize = 0, int pageSegMode = 3);

    /// <summary>
    /// Extract text blocks with their bounding boxes.
    /// </summary>
    /// <param name="image">Source image.</param>
    /// <param name="language">Language code (default: eng).</param>
    /// <param name="scale">Scale factor (default: 1.0).</param>
    /// <param name="whitelist">Allowed characters (default: null).</param>
    /// <param name="threshold">Binary threshold (0=disabled).</param>
    /// <param name="invert">Invert colors before threshold.</param>
    /// <param name="borderSize">Border pixels around text.</param>
    /// <param name="pageSegMode">Tesseract PSM: 3=auto, 7=single line, 8=single word, 13=raw line.</param>
    /// <returns>List of detected text blocks.</returns>
    List<OcrResultBlock> GetTextBlocks(IImage image, string language = "eng", double scale = 1.0, string whitelist = null, int threshold = 0, bool invert = false, int borderSize = 0, int pageSegMode = 3);

    /// <summary>
    /// Multi-threshold voting OCR: run at 3 thresholds, pick majority result.
    /// </summary>
    string GetTextWithVoting(IImage image, string language = "eng", double scale = 1.0,
        string whitelist = null, int baseThreshold = 150, bool invert = false, int borderSize = 12, int pageSegMode = 7);

    /// <summary>
    /// Segmentation-aware OCR: split merged digits using contour analysis, then OCR.
    /// </summary>
    string GetTextWithSegmentation(IImage image, string language = "eng", double scale = 1.0,
        string whitelist = null, int threshold = 150, bool invert = false, int borderSize = 12, int pageSegMode = 7);
}

public class OcrResultBlock
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public Rectangle Rect { get; set; }
    
    // Helper to get center point
    public System.Drawing.Point Center => new System.Drawing.Point(
        Rect.X + Rect.Width / 2, 
        Rect.Y + Rect.Height / 2);
}
