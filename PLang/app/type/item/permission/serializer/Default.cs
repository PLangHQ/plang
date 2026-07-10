namespace app.type.item.permission.serializer;

/// <summary>
/// Renderer for <see cref="app.type.item.permission.@this"/> — registers the grant as
/// a self-writing type so <c>Data.Normalize</c> passes it through to its own
/// <see cref="app.type.item.permission.@this.Write"/> (the <c>{actor, path, match,
/// verbs}</c> wire form) instead of reflecting its CLR properties. Uniform across
/// every format; the grant owns its wire shape.
/// </summary>
public static class Default
{
    public static void Write(global::app.type.item.permission.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        value.Write(writer);
    }
}
