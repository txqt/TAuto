using System;
using System.Threading;
using System.Threading.Tasks;
using TAuto.Core;
using TAuto.Core.Models;
using TAuto.Core.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace TAuto.Automation.Actions;

/// <summary>
/// Extract text from a specific region of the screen using OCR.
/// </summary>
public class ExtractTextAction : ActionBase
{
    public override string DisplayName => $"🔍 Extract Text to ${OutputVariable}";

    // Region to extract from: X, Y, Width, Height
    // If 0, uses full screen (not recommended usually)
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Variable name to store the extracted text.
    /// </summary>
    public string OutputVariable { get; set; } = "ExtractedText";

    /// <summary>
    /// Language for OCR (default "eng").
    /// </summary>
    public string Language { get; set; } = "eng";

    /// <summary>
    /// If true, trims whitespace and newlines.
    /// </summary>
    public bool Trim { get; set; } = true;

    /// <summary>
    /// Scale factor for image pre-processing (default 1.0).
    /// </summary>
    public double Scale { get; set; } = 1.0;

    /// <summary>
    /// Optional whitelist of allowed characters (e.g. "0123456789").
    /// </summary>
    public string Whitelist { get; set; }

    /// <summary>
    /// Optional: Log the extracted text automatically.
    /// </summary>
    public bool LogResult { get; set; } = true;

    /// <summary>
    /// Binary threshold (0-255). 0 = disabled. Typical: 100-180.
    /// When > 0, enables full preprocessing: Resize + Grayscale + Threshold + Border.
    /// </summary>
    public int Threshold { get; set; } = 0;

    /// <summary>
    /// If true, invert colors before thresholding (use for light text on dark background).
    /// </summary>
    public bool Invert { get; set; } = false;

    /// <summary>
    /// Border pixels to add around text (helps Tesseract with edge characters). Default: 12.
    /// </summary>
    public int BorderSize { get; set; } = 12;

    /// <summary>
    /// Tesseract Page Segmentation Mode.
    /// 3 = Fully automatic (default), 7 = Single text line, 8 = Single word.
    /// Use PSM 7 for individual number fields for best accuracy.
    /// </summary>
    public int PageSegMode { get; set; } = 3;

    /// <summary>
    /// If true, run OCR at multiple thresholds and pick the majority result (voting).
    /// Best for numeric fields where single-digit errors are common.
    /// </summary>
    public bool UseVoting { get; set; } = false;

    /// <summary>
    /// If true, use contour-based digit segmentation to split merged digits before OCR.
    /// Solves the "77 → 7" problem at low resolutions.
    /// </summary>
    public bool UseSegmentation { get; set; } = false;

    /// <summary>
    /// If set to a directory path, saves debug images (full screenshot + cropped region) there.
    /// Useful for verifying coordinate accuracy.
    /// </summary>
    public string DebugSavePath { get; set; }

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (context.Ocr == null)
            return ActionResult.Fail("OCR Service not available");

        // 1. Capture Screen
        await context.UpdateScreenCaptureAsync(force: true);
        if (context.LastScreenCapture == null)
            return ActionResult.Fail("Failed to capture screen");

        // 2. Crop Image
        IImage source = context.LastScreenCapture;
        if (Width > 0 && Height > 0)
        {
            try 
            {
                // Ensure bounds
                int rawX = Math.Max(0, X);
                int rawY = Math.Max(0, Y);
                if (rawX + Width > source.Width) Width = source.Width - rawX;
                if (rawY + Height > source.Height) Height = source.Height - rawY;

                if (Width <= 0 || Height <= 0)
                    return ActionResult.Fail("Invalid crop region");

                // Crop using ImageSharp if available (if using ImageWrapper)
                if (source is ImageWrapper wrapper)
                {
                    var cropped = wrapper.InnerImage.Clone(x => x.Crop(new Rectangle(rawX, rawY, Width, Height)));
                    source = new ImageWrapper(cropped);
                }
                else
                {
                    return ActionResult.Fail("Source image does not support cropping.");
                }

                // Debug: save images to verify coordinates
                if (!string.IsNullOrEmpty(DebugSavePath))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(DebugSavePath);
                        var ts = DateTime.Now.ToString("HHmmss");
                        // Save full screenshot
                        context.LastScreenCapture.Save(System.IO.Path.Combine(DebugSavePath, $"{ts}_{OutputVariable}_full_{context.LastScreenCapture.Width}x{context.LastScreenCapture.Height}.png"));
                        // Save cropped region
                        source.Save(System.IO.Path.Combine(DebugSavePath, $"{ts}_{OutputVariable}_crop_{rawX},{rawY}_{Width}x{Height}.png"));
                    }
                    catch { /* ignore debug save errors */ }
                }
            }
            catch (Exception ex)
            {
                return ActionResult.Fail($"Crop failed: {ex.Message}");
            }
        }

        // 3. Perform OCR
        try
        {
            // Route to the appropriate OCR method based on flags
            string text;
            if (UseVoting)
            {
                text = context.Ocr.GetTextWithVoting(source, Language, Scale, Whitelist, Threshold > 0 ? Threshold : 150, Invert, BorderSize, PageSegMode);
            }
            else if (UseSegmentation)
            {
                text = context.Ocr.GetTextWithSegmentation(source, Language, Scale, Whitelist, Threshold > 0 ? Threshold : 150, Invert, BorderSize, PageSegMode);
            }
            else
            {
                text = context.Ocr.GetText(source, Language, Scale, Whitelist, Threshold, Invert, BorderSize, PageSegMode);
            }
            
            if (Trim && text != null)
                text = text.Trim();

            // 4. Store Result
            context.SetVariable(OutputVariable, text ?? string.Empty);
            
            if (LogResult)
                context.Logger?.Info($"OCR [{OutputVariable}]: {text}");

            return ActionResult.Ok(new System.Drawing.Point(X, Y)); // Return location
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"OCR failed: {ex.Message}");
        }
    }

    // SaveBitmapSource removed, using IImage.Save() instead.
}
