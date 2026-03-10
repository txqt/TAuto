namespace TAuto.Core.Imaging;

public enum ImageFormat
{
    Png,
    Jpeg,
    Bmp
}

/// <summary>
/// Platform-agnostic image abstraction to prevent dependency on Windows presentation foundation (WPF)
/// </summary>
public interface IImage : IDisposable
{
    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets the raw pixel data if needed for direct access.
    /// Format defines the byte layout (e.g. BGRA32).
    /// </summary>
    byte[] GetPixelData();
    
    /// <summary>
    /// Saves the image to a file path.
    /// </summary>
    void Save(string filePath);

    /// <summary>
    /// Saves the image to a stream in the specified format asynchronously.
    /// </summary>
    Task SaveAsync(Stream stream, ImageFormat format, CancellationToken cancellationToken = default);
}
