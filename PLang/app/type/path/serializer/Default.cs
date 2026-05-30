namespace app.type.path.serializer;

/// <summary>
/// Wire renderer for <see cref="@this"/> — emits the path's portable
/// <c>Relative</c> string (falling back to <c>Raw</c> / <c>Absolute</c> when
/// Context isn't wired, mirroring the legacy <c>JsonConverter.Write</c>).
///
/// <para>One file, one decision: every wire format renders a path the same
/// way. The renderer dispatcher registers this class as
/// <c>(path, "*")</c>. The legacy <see cref="app.type.path.JsonConverter"/>
/// stays alive because STJ deserialization (Read) still routes through it
/// — the dispatch table only covers the write side. Deleting the legacy
/// converter is a follow-up that needs every STJ path-typed read to migrate
/// to <c>path.@this.Resolve(string, context)</c>.</para>
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
}
