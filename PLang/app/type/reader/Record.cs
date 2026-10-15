namespace app.type.reader;

/// <summary>
/// The fallback reader for a type that ships no reader of its own (decision 789): the item owns how it's made,
/// so the stored value is read as its generic record — an object into a plang <c>dict</c>, an array into a
/// <c>list</c>, a scalar into its plang scalar — and the type makes itself from that record
/// (<c>type.Make</c> → the item's own <c>ICreate.Create</c>). A type that can't be made from a record declines,
/// and the decline is the read's answer, thrown to the read boundary (a settings class whose stored row is of an
/// older shape surfaces <c>SettingsUnreadable</c> this way).
/// </summary>
internal sealed class Record : global::app.type.reader.ITypeReader
{
    private readonly string _type;
    public Record(string type) => _type = type;

    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this(_type, kind);
        global::app.type.item.@this record = reader.Peek() switch
        {
            global::app.type.format.TokenKind.Object => new global::app.type.item.dict.serializer.Reader().Read(ref reader, null, ctx),
            global::app.type.format.TokenKind.Array => new global::app.type.item.list.serializer.Reader().Read(ref reader, null, ctx),
            global::app.type.format.TokenKind.String => global::app.type.item.@this.Create(reader.String(), ctx.Context),
            global::app.type.format.TokenKind.Number => global::app.type.item.@this.Create(reader.Number(), ctx.Context),
            global::app.type.format.TokenKind.Bool => global::app.type.item.@this.Create(reader.Bool(), ctx.Context),
            _ => global::app.type.item.@this.Create(null, ctx.Context),
        };
        var data = new global::app.data.@this("", context: ctx.Context);
        // Made, not Make: a settings class is an ICreate<setting> subtype whose own Make declines — its base's
        // Create makes it by its declared.ClrType (decision 789).
        var made = ctx.Context.App.type.list[_type].Made(record, data);
        // A type that CAN'T be made from its record fails loudly (789) — its own Create said why on the carrier.
        // A clean decline (null with no error — a bare setting node that reads as absent) is the null value.
        if (made is null)
            return data.Error is { } err
                ? throw new global::app.error.AppException(err)
                : new global::app.type.item.@null.@this(_type, kind);
        return made;
    }
}
