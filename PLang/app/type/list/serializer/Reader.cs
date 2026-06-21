namespace app.type.list.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.list.@this"/> — the list streams its own elements off the
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
        if (reader.Null()) return new global::app.type.@null.@this("list", kind);
        reader.BeginArray();
        var list = new global::app.type.list.@this();
        while (reader.NextElement())
            list.AddRaw(global::app.type.item.serializer.json.ReadSlot(ref reader, ctx));
        reader.EndArray();
        return list;
    }
}
