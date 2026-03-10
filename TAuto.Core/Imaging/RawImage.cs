using System.IO;

namespace TAuto.Core.Imaging;

/// <summary>
/// A simple, uncompressed pixel-based implementation of IImage.
/// Pixel format is usually assumed to be BGRA32 (4 bytes per pixel).
/// </summary>
public class RawImage : IImage
{
    private const int DefaultDpi = 2835; // ~72 DPI in pixels per meter
    private readonly byte[] _pixels;
    
    public int Width { get; }
    public int Height { get; }

    public RawImage(int width, int height, byte[] pixels)
    {
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public byte[] GetPixelData()
    {
        return _pixels;
    }

    public void Save(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        Save(fs, ImageFormat.Bmp);
    }

    public void Save(Stream stream, ImageFormat format)
    {
        // Minimal BMP implementation for RawImage
        using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        
        // BMP Header
        int fileSize = 54 + _pixels.Length;
        bw.Write('B');
        bw.Write('M');
        bw.Write(fileSize);
        bw.Write(0); // Reserved
        bw.Write(54); // Offset to pixel array
        
        // DIB Header
        bw.Write(40); // DIB Header size
        bw.Write(Width);
        bw.Write(Height);
        bw.Write((short)1); // Planes
        bw.Write((short)32); // Bits per pixel
        bw.Write(0); // Compression
        bw.Write(_pixels.Length); // Image size
        bw.Write(DefaultDpi); // Horizontal resolution
        bw.Write(DefaultDpi); // Vertical resolution
        bw.Write(0); // Colors in color table
        bw.Write(0); // Important color count
        
        // Pixel data (BMP bottoms-up)
        int rowStride = Width * 4;
        for (int y = Height - 1; y >= 0; y--)
        {
            bw.Write(_pixels, y * rowStride, rowStride);
        }
    }

    public Task SaveAsync(Stream stream, ImageFormat format, CancellationToken cancellationToken = default)
    {
        // Since the current implementation is already efficient with BinaryWriter and memory streams,
        // and doesn't involve heavy external CPU/IO outside of the stream itself, 
        // we wrap it in a Task for interface consistency. 
        // For RawImage, actual async IO happens at the stream level if it's a FileStream wrapper.
        Save(stream, format);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // No unmanaged resources
    }
}
