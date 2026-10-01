namespace app.type.item.status.serializer;

/// <summary>Typed pull reader for <see cref="app.type.item.status.@this"/>: a status writes itself as its dict
/// (<c>{code, text}</c>); a bare number is its code. The status makes itself from what is read.</summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("status", kind);
        var written = global::app.type.item.@this.Create(new global::app.type.item.serializer.json(ctx.Context).Read(ref reader, ctx), ctx.Context);
        var carrier = new global::app.data.@this("", written, context: ctx.Context);
        // a status that refuses what was written says why, at the read boundary
        return (global::app.type.item.@this?)global::app.type.item.status.@this.Create(written, carrier.Type, carrier)
            ?? throw new global::app.error.DeclinedException(carrier.Error!);
    }
}
