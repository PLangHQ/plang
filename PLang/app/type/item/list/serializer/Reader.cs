namespace app.type.item.list.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.list.@this"/> — the list streams its own elements off the
/// single decode pass (store raw, type on read). Each slot is read raw via
/// <see cref="app.type.item.serializer.json.ReadSlot"/>: a scalar streams with no
/// DOM, a nested container / <c>@schema:data</c> element narrows through the parser.
/// The element walk lives on the container, not in Wire.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("list", kind);
        reader.BeginArray();
        var parser = new global::app.type.item.serializer.json(ctx.Context);
        var list = new global::app.type.item.list.@this(ctx.Context);
        // An authored list (ctx carries "plang") re-resolves its `%ref%` string leaves on read —
        // list.@this.Value → Resolve. A runtime-ingest read (ctx.Template null) stays literal.
        if (ctx.Template != null) list.Template = ctx.Template;
        while (reader.NextElement())
            list.AddRaw(parser.ReadSlot(ref reader, ctx));
        reader.EndArray();
        return list;
    }
}
