using System.IO;

namespace TAuto.Core.Imaging;

/// <summary>
/// A simple, uncompressed pixel-based implementation of IImage.
/// Pixel format is usually assumed to be BGRA32 (4 bytes per pixel).
/// </summary>
public class RawImage : IImage
{
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
        // For debugging/logs. In a real system you'd use a library like ImageSharp.
        // For this minimal cross-platform implementation, we just write a simple BMP file
        // since WPF BitmapEncoder is unavailable here.
        
        using var fs = new FileStream(filePath, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        
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
        bw.Write(2835); // Horizontal resolution
        bw.Write(2835); // Vertical resolution
        bw.Write(0); // Colors in color table
        bw.Write(0); // Important color count
        
        // Pixel data (BMP expects bottoms-up, but for raw debug dumps, writing 
        // raw bytes is sometimes acceptable. However, we'll write rows bottom-up for proper BMP format if BGRA).
        int rowStride = Width * 4;
        for (int y = Height - 1; y >= 0; y--)
        {
            bw.Write(_pixels, y * rowStride, rowStride);
        }
    }

    public void Dispose()
    {
        // No unmanaged resources
    }
}
