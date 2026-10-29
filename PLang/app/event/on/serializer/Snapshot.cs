namespace app.@event.on.serializer;

/// <summary>
/// The events bound on one item, read in the snapshot view: the record they were written as
/// (<c>{item, start: {before: [event]}}</c>) comes in, and each event is bound again on the reading actor's events
/// (<see cref="list.@this.Read"/>); the item's moments come out. A record that doesn't read is refused, thrown to the
/// read boundary.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>An item's moments are an object.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.Object;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var context = ctx.Context;
        var record = (global::app.type.item.dict.@this)new global::app.type.item.dict.serializer.Reader().Read(ref reader, null, ctx);
        return context.Actor!.@event.Read(record, context);
    }
}
