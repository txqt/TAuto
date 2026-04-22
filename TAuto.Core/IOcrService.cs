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
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Extracted text string.</returns>
    Task<string> GetTextAsync(IImage image, string language = "eng", double scale = 1.0, string? whitelist = null, int threshold = 0, bool invert = false, int borderSize = 0, int pageSegMode = 3, CancellationToken ct = default);

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
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of detected text blocks.</returns>
    Task<List<OcrResultBlock>> GetTextBlocksAsync(IImage image, string language = "eng", double scale = 1.0, string? whitelist = null, int threshold = 0, bool invert = false, int borderSize = 0, int pageSegMode = 3, CancellationToken ct = default);

    /// <summary>
    /// Multi-threshold voting OCR: run at 3 thresholds, pick majority result.
    /// </summary>
    Task<string> GetTextWithVotingAsync(IImage image, string language = "eng", double scale = 1.0,
        string? whitelist = null, int baseThreshold = 150, bool invert = false, int borderSize = 12, int pageSegMode = 7, CancellationToken ct = default);

    /// <summary>
    /// Segmentation-aware OCR: split merged digits using contour analysis, then OCR.
    /// </summary>
    Task<string> GetTextWithSegmentationAsync(IImage image, string language = "eng", double scale = 1.0,
        string? whitelist = null, int threshold = 150, bool invert = false, int borderSize = 12, int pageSegMode = 7, CancellationToken ct = default);
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
