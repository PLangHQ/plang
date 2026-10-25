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

    /// <summary>An encoded picture (PNG, JPEG) read; bytes that read as no picture are an AppException carrying
    /// BadFrame (the decoder's own exception its cause, when it threw one).</summary>
    internal Bitmap(byte[] encoded)
    {
        var info = SkiaSharp.SKBitmap.DecodeBounds(encoded);
        SkiaSharp.SKBitmap? decoded = null;
        Exception? cause = null;
        if (info.Width > 0 && info.Height > 0)
            try
            {
                decoded = SkiaSharp.SKBitmap.Decode(encoded,
                    new SkiaSharp.SKImageInfo(info.Width, info.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul));
            }
            catch (Exception ex)
            {
                // the decoder throws a plain Exception on a picture it can't make (a header past what its data holds)
                cause = ex;
            }
        if (decoded == null)
        {
            var error = new global::app.error.@this("Not an image: " + (cause?.Message ?? "no image format reads the frame"), "BadFrame", 400);
            if (cause != null) error.list.Add(global::app.error.@this.FromException(cause));
            throw new global::app.error.AppException(error, cause);
        }
        using (decoded) (Width, Height, Pixels) = (decoded.Width, decoded.Height, decoded.Bytes);
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
