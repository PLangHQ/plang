namespace app.type.item.dict.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.dict.@this"/> — the dict streams its own entries off the
/// single decode pass (store raw, type on read). Each entry value is read raw via
/// <see cref="app.type.item.serializer.json.ReadSlot"/>: a scalar streams with no
/// DOM, a nested container / <c>@schema:data</c> value narrows through the parser.
/// The entry walk lives on the container, not in Wire.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("dict", kind);
        reader.BeginObject();
        var parser = new global::app.type.item.serializer.json(ctx.Context);
        var dict = new global::app.type.item.dict.@this(ctx.Context);
        // An authored dict (ctx carries "plang") re-resolves its `%ref%` string leaves on read —
        // dict.@this.Value → Resolve. A runtime-ingest read (ctx.Template null) stays literal.
        if (ctx.Template != null) dict.Template = ctx.Template;
        while (reader.NextName(out var name))
            dict.Set(name, parser.ReadSlot(ref reader, ctx));
        reader.EndObject();
        return dict;
    }
}
