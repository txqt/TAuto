using System.IO;

namespace TAuto.Core.Imaging;

/// <summary>
/// A simple, uncompressed pixel-based implementation of IImage.
/// Pixel format is usually assumed to be BGRA32 (4 bytes per pixel).
/// </summary>
public class RawImage : IImage
{
    private const int DefaultDpi = 2835; // ~72 DPI in pixels per meter
    private byte[]? _pixels;
    private bool _disposed;
    
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _pixels!;
    }

    public IImage Clone()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var newPixels = new byte[_pixels!.Length];
        Buffer.BlockCopy(_pixels, 0, newPixels, 0, _pixels.Length);
        return new RawImage(Width, Height, newPixels);
    }

    public void CopyPixelDataTo(byte[] destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Buffer.BlockCopy(_pixels!, 0, destination, 0, Math.Min(_pixels!.Length, destination.Length));
    }

    public void Save(string filePath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var fs = new FileStream(filePath, FileMode.Create);
        Save(fs, ImageFormat.Bmp);
    }

    public void Save(Stream stream, ImageFormat format)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Minimal BMP implementation for RawImage
        using var bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        
        // BMP Header
        int fileSize = 54 + _pixels!.Length;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        Save(stream, format);
        return Task.CompletedTask;
    }

    /// <summary>
    /// AUDIT FIX (CRITICAL-3): Release the LOH byte[] reference immediately.
    /// RawImage pixels can be 8MB+ at 1080p. A no-op Dispose() keeps these
    /// pinned in Gen2/LOH, causing fragmentation and eventual OOM after 24h+.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pixels = null; // Release LOH reference for deterministic GC collection
    }
}
