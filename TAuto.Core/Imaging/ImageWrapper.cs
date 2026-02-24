using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;

namespace TAuto.Core.Imaging;

/// <summary>
/// Platform-agnostic image implementation wrapping SixLabors.ImageSharp.
/// Ensures that core logic isn't tightly coupled to WPF's BitmapSource.
/// </summary>
public class ImageWrapper : IImage
{
    private readonly Image<Bgra32> _image;
    private bool _disposed;

    public int Width => _image.Width;
    public int Height => _image.Height;

    public Image<Bgra32> InnerImage => _image;

    public ImageWrapper(Image<Bgra32> image)
    {
        _image = image;
    }

    public ImageWrapper(int width, int height, byte[] bgraPixelData)
    {
        _image = Image.LoadPixelData<Bgra32>(bgraPixelData, width, height);
    }

    public static ImageWrapper Load(string filePath)
    {
        return new ImageWrapper(Image.Load<Bgra32>(filePath));
    }

    public static ImageWrapper? Decode(byte[] data)
    {
        try
        {
            return new ImageWrapper(Image.Load<Bgra32>(data));
        }
        catch
        {
            return null;
        }
    }

    public byte[] GetPixelData()
    {
        var pixelData = new byte[Width * Height * 4];
        _image.CopyPixelDataTo(pixelData);
        return pixelData;
    }

    public void Save(string filePath)
    {
        _image.Save(filePath);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _image.Dispose();
    }
}
