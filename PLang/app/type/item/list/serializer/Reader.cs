namespace app.type.item.list.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.list.@this"/> — the list streams its own elements off the
/// single decode pass (store raw, type on read). Each slot is read raw via
/// <see cref="app.type.item.serializer.json.Entry"/>: a scalar streams with no
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
        var list = new global::app.type.item.list.@this();
        // An authored list (ctx carries "plang") re-resolves its `%ref%` string leaves on read —
        // list.@this.Value → Resolve. A runtime-ingest read (ctx.Template null) stays literal.
        if (ctx.Template != null) list.Template = ctx.Template;
        // The element type rides as this list's kind (list<action> = {list, kind:action}). If the
        // element type owns a reader, a bare element reads ITSELF through it (a .pr's action → its
        // params). An element the list wrote as its Data row ({type, value} — how a list writes every
        // element) is that Data, as every container's typed entry is: its own type says what it is.
        // Otherwise the element streams as a raw slot (a list of scalars / dicts). Generic — the list
        // is about type X, never a specific element.
        var elementReader = kind is { } elementType
            ? ctx.Context.App.type.list.Reader.Typed(elementType, null)
            : null;
        while (reader.NextElement())
        {
            if (elementReader is null) list.AddRaw(parser.Entry(ref reader, ctx));
            // a typed entry is an object; any other token is a bare element
            else if (reader.Peek() != global::app.channel.serializer.TokenKind.Object) list.AddRaw(elementReader.Read(ref reader, null, ctx));
            else list.AddRaw(Element(reader.RawValue(), elementReader, parser, ctx));
        }
        reader.EndArray();
        return list;
    }

    // An object element: the Data row the list wrote, else a bare element its type reads over its bytes.
    private object? Element(byte[] raw, global::app.type.reader.ITypeReader element,
        global::app.type.item.serializer.json parser, global::app.type.reader.ReadContext ctx)
    {
        if (parser.Typed(raw) is { } row) return row;
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var bare = new global::app.channel.serializer.json.Reader(utf8, raw);
        return element.Read(ref bare, null, ctx);
    }
}
