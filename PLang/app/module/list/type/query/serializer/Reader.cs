namespace app.module.list.type.query.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.module.list.type.query.@this"/>:
/// a query writes itself as its dict, so the value is pulled as a dict off the single pass, and the query is made
/// from that dict — the one way a query is made. One that doesn't read is the query's null.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var nothing = new global::app.type.item.@null.@this("query", kind);
        if (reader.Null()) return nothing;
        var written = global::app.type.item.@this.Create(new global::app.type.item.serializer.json(ctx.Context).Read(ref reader, ctx), ctx.Context);
        var carrier = new global::app.data.@this("", nothing, context: ctx.Context);
        return (global::app.type.item.@this?)global::app.module.list.type.query.@this.Create(written, carrier.Type, carrier) ?? nothing;
    }
}
