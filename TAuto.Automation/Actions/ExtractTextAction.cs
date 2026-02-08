using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using TAuto.Core;

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
    /// Optional: Log the extracted text automatically.
    /// </summary>
    public bool LogResult { get; set; } = true;

    public override async Task<ActionResult> ExecuteAsync(ScriptContext context, CancellationToken ct)
    {
        if (context.Ocr == null)
            return ActionResult.Fail("OCR Service not available");

        // 1. Capture Screen
        await context.UpdateScreenCaptureAsync();
        if (context.LastScreenCapture == null)
            return ActionResult.Fail("Failed to capture screen");

        // 2. Crop Image
        BitmapSource source = context.LastScreenCapture;
        if (Width > 0 && Height > 0)
        {
            try 
            {
                // Ensure bounds
                int rawX = Math.Max(0, X);
                int rawY = Math.Max(0, Y);
                if (rawX + Width > source.PixelWidth) Width = source.PixelWidth - rawX;
                if (rawY + Height > source.PixelHeight) Height = source.PixelHeight - rawY;

                if (Width <= 0 || Height <= 0)
                    return ActionResult.Fail("Invalid crop region");

                source = new CroppedBitmap(source, new Int32Rect(rawX, rawY, Width, Height));
            }
            catch (Exception ex)
            {
                return ActionResult.Fail($"Crop failed: {ex.Message}");
            }
        }

        // 3. Perform OCR
        try
        {
            string text = context.Ocr.GetText(source, Language);
            
            if (Trim && text != null)
                text = text.Trim();

            // 4. Store Result
            context.SetVariable(OutputVariable, text ?? string.Empty);
            
            if (LogResult)
                context.Logger?.Info($"OCR [{OutputVariable}]: {text}");

            return ActionResult.Ok(new Point(X, Y)); // Return location
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"OCR failed: {ex.Message}");
        }
    }
}
