namespace app.type.item.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for the <c>item</c>
/// shape — a json payload of unknown shape (the universal value). Same streaming
/// decode as the <c>object</c> reader (born-native names an unknown-shape json
/// payload <c>item</c>); the value pulls off the single pass via the shared
/// <see cref="json"/> parser, no DOM.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("item", kind);
        var parser = new json(ctx.Context);
        return global::app.type.@this.Create(parser.ReadSlot(ref reader, ctx), ctx.Context);
    }
}
