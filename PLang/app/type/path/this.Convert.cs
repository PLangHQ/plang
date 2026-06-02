namespace app.type.path;

public abstract partial class @this
{
    /// <summary>
    /// OBP: <c>path</c> owns how a path value is built from a raw string. Routes
    /// through the per-App scheme registry, which dispatches to the right subclass
    /// (file → FilePath, http/https → HttpPath, …) by the string's scheme prefix.
    /// No I/O — just constructs the path. A value already a path passes through.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        actor.context.@this context)
    {
        if (value is null) return global::app.data.@this.Ok(value);
        if (value is @this) return global::app.data.@this.Ok(value);
        if (value is not string raw)
            return global::app.data.@this.FromError(new global::app.error.Error(
                $"Cannot convert {value.GetType().Name} to path.", "PathConversionFailed", 400));

        try
        {
            return global::app.data.@this.Ok(context.App.Type.Scheme.From(raw, context));
        }
        catch (scheme.SchemeNotRegistered snr)
        {
            return global::app.data.@this.FromError(new global::app.error.Error(
                snr.Message, "SchemeNotRegistered", 400)
                { FixSuggestion = $"Register a factory for scheme '{snr.Scheme}' via app.Type.Scheme.Register, or use a bare/file:// path." });
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            return global::app.data.@this.FromError(new global::app.error.Error(
                ex.Message, "PathConstructionFailed", 400));
        }
    }
}
