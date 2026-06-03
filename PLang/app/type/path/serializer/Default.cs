namespace app.type.path.serializer;

/// <summary>
/// Wire renderer for <see cref="@this"/> — emits the path's portable
/// <c>Relative</c> string (falling back to <c>Raw</c> / <c>Absolute</c> when
/// Context isn't wired, mirroring the legacy <c>JsonConverter.Write</c>).
///
/// <para>One file, one decision: every wire format renders a path the same
/// way. The renderer dispatcher registers this class as <c>(path, "*")</c>,
/// and the reader registry registers <see cref="Read"/> as the path decode.
/// Mid-graph STJ path fields are served by the single json
/// <c>Converter</c>, which routes to <see cref="Read"/> through the reader
/// registry — the legacy <c>path.JsonConverter</c> is gone.</para>
/// </summary>
public static class Default
{
    public static void Write(global::app.type.path.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }

        string? wire = null;
        if (value.Context != null)
        {
            try { wire = value.Relative; }
            catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
            {
                wire = null;
            }
        }
        wire ??= !string.IsNullOrEmpty(value.Raw) ? value.Raw : value.Absolute;
        writer.String(wire);
    }

    /// <summary>
    /// Read mirror of <see cref="Write"/> — the decode logic that used to live
    /// in <c>app.type.path.JsonConverter.Read</c>. A path's raw wire form is its
    /// portable <c>Relative</c> string; with a Context it resolves scheme-correct
    /// and fully wired via <see cref="app.type.path.@this.Resolve(string, actor.context.@this)"/>,
    /// without one it falls back to a bare file-scheme stub (the Authorize callers
    /// then explode on it — that is the contract).
    /// </summary>
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        if (raw is not string s || string.IsNullOrEmpty(s)) return null;
        if (ctx.Context != null) return global::app.type.path.@this.Resolve(s, ctx.Context);
        return new global::app.type.path.file.@this(s, context: null) { Raw = s };
    }
}
