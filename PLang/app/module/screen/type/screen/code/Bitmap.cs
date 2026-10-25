namespace app.module.screen.type.screen.code;

/// <summary>
/// A picture as straight (unpremultiplied) BGRA pixels, top row first, <see cref="Width"/> × <see cref="Height"/>:
/// what a screen shows. It reads an encoded picture (PNG, JPEG) into itself and writes itself as PNG (SkiaSharp).
/// </summary>
internal sealed class Bitmap
{
    internal int Width { get; }
    internal int Height { get; }
    internal byte[] Pixels { get; }

    /// <summary>A blank one, its pixels pinned: a canvas that lives as long as its screen.</summary>
    internal Bitmap(int width, int height) : this(width, height, GC.AllocateArray<byte>(width * height * 4, pinned: true)) { }

    /// <summary>An encoded picture (PNG, JPEG) read; bytes no image format reads are a FormatException.</summary>
    internal Bitmap(byte[] encoded)
    {
        var info = SkiaSharp.SKBitmap.DecodeBounds(encoded);
        if (info.Width <= 0 || info.Height <= 0) throw new FormatException("the frame is no image format");
        using var decoded = SkiaSharp.SKBitmap.Decode(encoded,
            new SkiaSharp.SKImageInfo(info.Width, info.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul))
            ?? throw new FormatException("the frame's pixels can't be read");
        (Width, Height, Pixels) = (decoded.Width, decoded.Height, decoded.Bytes);
    }

    /// <summary>A copy of <paramref name="bitmap"/>, its pixels its own.</summary>
    internal Bitmap(Bitmap bitmap) : this(bitmap.Width, bitmap.Height, (byte[])bitmap.Pixels.Clone()) { }

    private Bitmap(int width, int height, byte[] pixels) => (Width, Height, Pixels) = (width, height, pixels);

    /// <summary>It as a PNG image.</summary>
    internal byte[] Png
    {
        get
        {
            var info = new SkiaSharp.SKImageInfo(Width, Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul);
            using var pixels = SkiaSharp.SKData.CreateCopy(Pixels);
            using var image = SkiaSharp.SKImage.FromPixels(info, pixels, info.RowBytes);
            using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return png.ToArray();
        }
    }
}
