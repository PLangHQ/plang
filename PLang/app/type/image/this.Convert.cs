namespace app.type.image;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>image</c> owns how a value of itself is built. A path-string mints a
    /// LAZY handle — <c>.Path</c> is set and nothing is read; content materializes on
    /// the first <see cref="BytesAsync"/>. The proving instance for reference-fundamental
    /// laziness (audio/video follow the same shape, each with its own <c>Convert</c>).
    ///
    /// <para>Returns <c>null</c> to decline any shape that isn't a path-string (a raw
    /// <c>byte[]</c> blob, etc.) — those are constructed at their own seam (file.read's
    /// image lift) or kept raw by the caller. Declining lets the dispatcher fall through.</para>
    /// </summary>
    public static global::app.data.@this? Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is @this) return global::app.data.@this.Ok(value);
        if (value is not string raw) return null;

        try
        {
            var path = context.App.Type.Scheme.From(raw, context);
            return global::app.data.@this.Ok(new @this(path));
        }
        catch (global::app.type.path.scheme.SchemeNotRegistered snr)
        {
            return global::app.data.@this.FromError(new global::app.error.Error(
                snr.Message, "SchemeNotRegistered", 400)
                { FixSuggestion = $"Register a factory for scheme '{snr.Scheme}', or use a bare/file:// path." });
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            return global::app.data.@this.FromError(new global::app.error.Error(
                ex.InnerException?.Message ?? ex.Message, "PathHandleConstructionFailed", 400));
        }
    }
}
