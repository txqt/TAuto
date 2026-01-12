using System.Windows.Media.Imaging;

namespace TAuto.Core;

/// <summary>
/// Platform-independent computer vision interface.
/// </summary>
public interface IVisionService
{
    /// <summary>
    /// Find template image within source using template matching
    /// </summary>
    TemplateMatchResult FindTemplate(BitmapSource source, BitmapSource template, double threshold);
    
    /// <summary>
    /// Load template from file path
    /// </summary>
    BitmapSource? LoadTemplate(string path);
    
    /// <summary>
    /// Save image as template
    /// </summary>
    string SaveTemplate(BitmapSource image, string name);
    
    /// <summary>
    /// Get all available templates
    /// </summary>
    string[] GetSavedTemplates();
    
    /// <summary>
    /// Delete template by path
    /// </summary>
    bool DeleteTemplate(string path);
}
