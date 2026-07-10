namespace app.type.item.number.serializer;

/// <summary>
/// Wire renderer for <see cref="app.type.item.number.@this"/> — the value knows its own kind, so it writes
/// itself through it. The 15-arm switch dissolved onto the kind classes (<c>type/number/kind/&lt;k&gt;</c>).
/// </summary>
public static class Default
{
    public static void Write(global::app.type.item.number.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        value.Kind.Write(value, writer);
    }
}
