using TAuto.Core.Imaging;

namespace TAuto.Core;

/// <summary>
/// Handles matching a template image against a source image.
/// </summary>
public interface ITemplateMatcher
{
    /// <summary>
    /// Find template image within source using template matching
    /// </summary>
    TemplateMatchResult FindTemplate(IImage source, IImage template, double threshold, string? templatePath = null, System.Drawing.Rectangle? roi = null, bool disableMultiScale = false);

    /// <summary>
    /// Find multiple templates against the same source frame with optional ROIs and fallback.
    /// </summary>
    TemplateMatchResult[] FindTemplates(IImage source, 
        (IImage Template, string? Path, double Threshold)[] templates, 
        System.Drawing.Rectangle? roi = null, 
        System.Drawing.Rectangle[]? regions = null, 
        bool fallbackFullscreen = false,
        bool disableMultiScale = false);
}

/// <summary>
/// Handles storage and retrieval of template images to/from disk or database.
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Load template from file path
    /// </summary>
    IImage? LoadTemplate(string path, string? baseDirectory = null);
    
    /// <summary>
    /// Save image as template
    /// </summary>
    string SaveTemplate(IImage image, string name);
    
    /// <summary>
    /// Get all available templates
    /// </summary>
    string[] GetSavedTemplates();
    
    /// <summary>
    /// Delete template by path
    /// </summary>
    bool DeleteTemplate(string path);

    /// <summary>
    /// Preloads templates into memory to avoid I/O delays during execution
    /// </summary>
    void PreloadTemplates(IEnumerable<string> paths, string? baseDirectory = null);
}

/// <summary>
/// Platform-independent computer vision interface.
/// Aggregates template matching, color detection, and repository operations.
/// </summary>
public interface IVisionService : ITemplateMatcher, IColorDetector, ITemplateRepository
{
}
