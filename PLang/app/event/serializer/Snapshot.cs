namespace app.@event.serializer;

/// <summary>
/// An event read in the snapshot view: its key comes in (<c>{item, name}</c>, what the snapshot wrote where a running
/// bound call pointed at it) and the event of that name in the resumed app's events for that item comes out — the item
/// read as what it says it is, then the event found through the app's read door, nothing made. An event the app binds
/// nothing on is refused (<c>EventNotFound</c>), thrown to the read boundary — never read as the class's shared event,
/// which holds no bindings.
/// </summary>
public sealed class Snapshot : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.View View => global::app.View.Snapshot;

    /// <summary>An event's key is an object.</summary>
    public bool Reads(global::app.type.format.TokenKind token) => token == global::app.type.format.TokenKind.Object;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind, global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var context = ctx.Context;
        var record = (global::app.type.item.dict.@this)new global::app.type.item.dict.serializer.Reader().Read(ref reader, null, ctx);
        if (record.Get("item", context) is not { } row
            || global::app.View.Snapshot.Make(row.Type ?? context.App.type.list[typeof(global::app.type.item.@this)], row.Peek(), context)
                is not { } item)
            throw new global::app.error.DeclinedException(new global::app.error.@this(
                "a saved event names no item it is an event of", "EventKeyIncomplete", 400));
        var name = record.Clr("name", typeof(string)) as string ?? "";
        // the event the app's events for the item made: one never bound on reads as its class's shared one, which is not it
        return context.App.@event[item] is { } events && events[name] is { } @event && ReferenceEquals(@event.on, events)
            ? @event
            : throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the saved event '{name}' of {item} is no event this app binds on", "EventNotFound", 404));
    }
}
