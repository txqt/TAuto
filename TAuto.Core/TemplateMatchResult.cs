using System.Drawing;

namespace TAuto.Core;

/// <summary>
/// Result of a template matching operation.
/// </summary>
public class TemplateMatchResult
{
    /// <summary>
    /// Whether the template was found in the source image.
    /// </summary>
    public bool Found { get; set; }
    
    /// <summary>
    /// The match confidence (0.0 - 1.0).
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Top-left location of the found template.
    /// </summary>
    public Point Location { get; set; }
    
    /// <summary>
    /// Center location of the found template.
    /// </summary>
    public Point CenterLocation { get; set; }
    
    /// <summary>
    /// Width of the matched template.
    /// </summary>
    public int TemplateWidth { get; set; }
    
    /// <summary>
    /// Height of the matched template.
    /// </summary>
    public int TemplateHeight { get; set; }
    
    /// <summary>
    /// Error message if matching failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
