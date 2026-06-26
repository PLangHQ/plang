namespace app.type.image;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>image</c> owns how a value of itself is built. A path-string mints a
    /// LAZY handle — <c>.Path</c> is set and nothing is read; content materializes on
    /// the first <see cref="BytesAsync"/>. The proving instance for reference-fundamental
    /// laziness (audio/video follow the same shape, each with its own <c>Convert</c>).
    ///
    /// <para>A raw <c>byte[]</c> declared <c>as image</c> is built here from its magic
    /// bytes — the explicit declaration is the ask, and <see cref="FromBytes"/> sniffs the
    /// mime. Bytes whose magic doesn't name an image decline (return <c>null</c>), letting
    /// the dispatcher fall through. Content off I/O is a different path: it rests as
    /// <c>binary</c>/kind and narrows through the kind's reader on access, never here.</para>
    /// </summary>
    public static global::app.data.@this? Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is @this) return context.Ok(value);
        // A byte[] declared `as image` becomes the image its magic bytes name — the
        // explicit declaration is the ask, and we already hold the bytes to sniff.
        if (value is byte[] bytes)
            return FromBytes(bytes) is { } image ? context.Ok(image) : null;
        if (value is not string raw) return null;

        try
        {
            var path = context.App.Type.Scheme.From(raw, context);
            return context.Ok(new @this(path));
        }
        catch (global::app.type.path.scheme.SchemeNotRegistered snr)
        {
            return context.Error(new global::app.error.Error(
                snr.Message, "SchemeNotRegistered", 400)
                { FixSuggestion = $"Register a factory for scheme '{snr.Scheme}', or use a bare/file:// path." });
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            return context.Error(new global::app.error.Error(
                ex.InnerException?.Message ?? ex.Message, "PathHandleConstructionFailed", 400));
        }
    }
}
