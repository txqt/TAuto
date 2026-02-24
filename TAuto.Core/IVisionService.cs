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
    TemplateMatchResult FindTemplate(IImage source, IImage template, double threshold, string? templatePath = null);
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
}

/// <summary>
/// Platform-independent computer vision interface.
/// Aggregates template matching and repository operations.
/// </summary>
public interface IVisionService : ITemplateMatcher, ITemplateRepository
{
}
