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
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this("list", kind), ctx.Context]);
        var parser = new global::app.type.item.serializer.json(ctx.Context);
        // An authored list (ctx carries "plang") is born a template and re-resolves its `%ref%` string
        // leaves on read — list.@this.Value → Resolve. A runtime-ingest read (ctx.Template null) stays literal.
        var list = new global::app.type.item.list.@this(
            ctx.Context.App.type.list[new global::app.type.@this("list", null, false, ctx.Template), ctx.Context]);
        // The element type rides as this list's kind (list<action> = {list, kind:action}). A bare element
        // reads ITSELF through the element type's reader — its own (a .pr's action → its params), else as
        // a record its Create makes (a list of messages holds messages). An element the list wrote as its
        // Data row ({type, value} — how a list writes every element) is that Data, as every container's
        // typed entry is: its own type says what it is. A list of no kind streams each element as a raw
        // slot. Generic — the list is about type X, never a specific element.
        var elementReader = kind is { } elementType
            ? ctx.Context.App.type.list.Reader.Reader(elementType, null, ctx.Context)
            : null;
        // a list given one value (a prompt written as text for a list of messages) holds that one element
        if (reader.Peek() != global::app.type.format.TokenKind.Array)
        {
            list.AddRaw(elementReader is null ? parser.Entry(ref reader, ctx) : elementReader.Read(ref reader, null, ctx));
            return list;
        }
        reader.BeginArray();
        while (reader.NextElement())
        {
            if (elementReader is null) list.AddRaw(parser.Entry(ref reader, ctx));
            // a typed entry is an object; any other token is a bare element
            else if (reader.Peek() != global::app.type.format.TokenKind.Object) list.AddRaw(elementReader.Read(ref reader, null, ctx));
            else list.AddRaw(Element(reader.RawValue(), elementReader, parser, ctx));
        }
        reader.EndArray();
        return list;
    }

    // An object element: the Data row the list wrote, else a bare element its type reads over its bytes.
    private object? Element(byte[] raw, global::app.type.reader.ITypeReader element,
        global::app.type.item.serializer.json parser, global::app.type.reader.ReadContext ctx)
    {
        if (parser.Typed(raw, ctx) is { } row) return row;
        var utf8 = new System.Text.Json.Utf8JsonReader(raw);
        utf8.Read();
        var bare = new global::app.type.item.kind.json.Reader(utf8, raw);
        return element.Read(ref bare, null, ctx);
    }
}
