namespace app.type.@object.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for the
/// <c>object</c> shape — hierarchical/tree data. Streams the structured value off
/// the single decode pass via the shared <see cref="app.type.item.serializer.json"/>
/// parser (object→native dict, array→native list, scalar→its wrapper; a
/// <c>@schema:data</c> element reconstructs as the Data it is), no json-string
/// round-trip, no DOM. The mirror of <see cref="json.Read"/>'s whole-payload decode
/// for the token path.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("object", kind);
        var parser = new global::app.type.item.serializer.json(ctx.Context);
        return ctx.Context.App.Type.Create(parser.ReadSlot(ref reader, ctx), ctx.Context);
    }
}
