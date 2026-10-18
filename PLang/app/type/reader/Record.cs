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
        if (reader.Null()) return new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this(_type, kind), ctx.Context]);
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
        // The type carries its kind to the item's Create, but keeps _type's OWN class — never resolving the kind
        // to a subclass. A {setting, kind:<path>} older-shape row (the path riding as the kind) thus reaches
        // setting.Create as the base setting WITH that kind, so it declines loudly (790) instead of reading as a
        // bare node. Dropping the kind (as before) made the row look like a node and read as absent — silent.
        var owner = ctx.Context.App.type.list[_type];
        var type = string.IsNullOrEmpty(kind) ? owner : new global::app.type.@this(_type, owner.ClrType, kind);
        // Construct, not Make: a settings class is an ICreate<setting> subtype whose own Make declines — its
        // base's Create makes it by its declared.ClrType (decision 789).
        var made = type.Construct(record, data);
        // A type that CAN'T be made from its record declines with the reason its own Create gave (789) — a
        // DeclinedException, which source.Value catches and answers softly on the binding (Success=false, the
        // error named), never thrown to the courier. A clean decline (null, no error — a bare setting node that
        // reads as absent) is the null value.
        if (made is null)
            return data.Error is { } err
                ? throw new global::app.error.DeclinedException(err)
                : new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this(_type, kind), ctx.Context]);
        return made;
    }
}
