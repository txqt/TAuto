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
    Task<TemplateMatchResult> FindTemplateAsync(IImage source, IImage template, double threshold, string? templatePath = null, System.Drawing.Rectangle? roi = null, bool disableMultiScale = false, CancellationToken ct = default);

    /// <summary>
    /// Find multiple templates against the same source frame with optional ROIs and fallback.
    /// </summary>
    Task<TemplateMatchResult[]> FindTemplatesAsync(IImage source, 
        (IImage Template, string? Path, double Threshold)[] templates, 
        System.Drawing.Rectangle? roi = null, 
        System.Drawing.Rectangle[]? regions = null, 
        bool fallbackFullscreen = false,
        bool disableMultiScale = false,
        CancellationToken ct = default);
}

/// <summary>
/// Handles storage and retrieval of template images to/from disk or database.
/// </summary>
public interface ITemplateRepository
{
    /// <summary>
    /// Load template from file path
    /// </summary>
    Task<IImage?> LoadTemplateAsync(string path, string? baseDirectory = null, CancellationToken ct = default);
    
    /// <summary>
    /// Save image as template
    /// </summary>
    Task<string> SaveTemplateAsync(IImage image, string name, CancellationToken ct = default);
    
    /// <summary>
    /// Get all available templates
    /// </summary>
    Task<string[]> GetSavedTemplatesAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Delete template by path
    /// </summary>
    Task<bool> DeleteTemplateAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Preloads templates into memory to avoid I/O delays during execution
    /// </summary>
    Task PreloadTemplatesAsync(IEnumerable<string> paths, string? baseDirectory = null, CancellationToken ct = default);
}

/// <summary>
/// Platform-independent computer vision interface.
/// Aggregates template matching, color detection, and repository operations.
/// </summary>
public interface IVisionService : ITemplateMatcher, IColorDetector, ITemplateRepository
{
}
