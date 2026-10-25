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

    /// <summary>An encoded picture (PNG, JPEG) read for a screen <paramref name="width"/> × <paramref name="height"/>.
    /// One that reads as no picture, or whose header claims more than the screen (refused before its pixels are
    /// made), is an AppException carrying BadFrame, the decoder's own exception its cause when it threw one.</summary>
    internal Bitmap(byte[] encoded, int width, int height)
    {
        var info = SkiaSharp.SKBitmap.DecodeBounds(encoded);
        // the sides compared one by one: no product of what the header claims is ever made
        var why = info.Width <= 0 || info.Height <= 0 ? "no image format reads the frame"
            : info.Width > width || info.Height > height ? $"the frame claims {info.Width}×{info.Height}, more than the screen's {width}×{height}"
            : null;
        Exception? cause = null;
        if (why == null)
            try
            {
                using var decoded = SkiaSharp.SKBitmap.Decode(encoded,
                    new SkiaSharp.SKImageInfo(info.Width, info.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Unpremul));
                if (decoded != null)
                {
                    (Width, Height, Pixels) = (decoded.Width, decoded.Height, decoded.Bytes);
                    return;
                }
                why = "the frame's pixels can't be read";
            }
            catch (Exception ex)
            {
                // the decoder throws a plain Exception on a picture it can't make (a header past what its data holds)
                cause = ex;
                why = ex.Message;
            }
        var error = new global::app.error.@this("Not an image: " + why, "BadFrame", 400);
        if (cause != null) error.list.Add(global::app.error.@this.FromException(cause));
        throw new global::app.error.AppException(error, cause);
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
