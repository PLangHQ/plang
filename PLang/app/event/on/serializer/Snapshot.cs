namespace app.@event.on.serializer;

/// <summary>
/// An item's moments read in the snapshot view: the record the moments wrote (<c>{item, start: {before: [event]}}</c>)
/// comes in, and the moments the resumed app holds for that item come out, the saved events read onto them
/// (<see cref="on.@this.Create"/>). A record that doesn't read is refused, thrown to the read boundary.
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
        var record = new global::app.type.item.dict.serializer.Reader().Read(ref reader, null, ctx);
        var read = new global::app.data.@this("", record, context: ctx.Context);
        return on.@this.Create(record, null, read)
            ?? throw new global::app.error.DeclinedException(read.Error!);
    }
}
