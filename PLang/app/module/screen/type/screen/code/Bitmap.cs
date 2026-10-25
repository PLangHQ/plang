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

    /// <summary>An encoded picture (PNG, JPEG) read, its pixels at most <paramref name="max"/>
    /// (<c>screen.setting.max</c>). One that reads as no picture, or whose header claims more (refused before its
    /// pixels are made), is an AppException carrying BadFrame, the decoder's own exception its cause when it threw one.</summary>
    internal Bitmap(byte[] encoded, global::app.type.item.size.@this max)
    {
        var info = SkiaSharp.SKBitmap.DecodeBounds(encoded);
        var why = info.Width <= 0 || info.Height <= 0 ? "no image format reads the frame"
            : !Fits(info.Width, info.Height, max)
                ? $"the frame claims {info.Width}×{info.Height}, more pixels than screen.setting.max ({max}) holds; raise it with --screen.setting.max=1GiB"
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

    /// <summary>Whether a picture <paramref name="width"/>×<paramref name="height"/> has pixels <paramref name="max"/>
    /// holds: the one rule for a picture and for each rectangle of a frame. Width × height fits a long for any two ints;
    /// × 4 is taken off the max instead, so nothing overflows.</summary>
    internal static bool Fits(int width, int height, global::app.type.item.size.@this max)
        => width >= 0 && height >= 0 && (long)width * height <= max.Value / 4;

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
