namespace app.@event.serializer;

/// <summary>
/// An event read in the snapshot view: its key comes in (<c>{action, n}</c>, what the snapshot wrote where a running
/// event's call pointed at it) and the event comes out — the <c>on.event</c> action that made it, read as an action, then
/// the n-th of the events that action made in the resuming actor's events, the order the key was numbered in. A key no event
/// answers is refused (<c>EventNotFound</c>), thrown to the read boundary.
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
        if (record.Get("action", context) is not { } row
            || global::app.View.Snapshot.Make(context.App.type.list[typeof(global::app.goal.step.action.@this)], row.Peek(), context)
                is not global::app.goal.step.action.@this action
            || record.Clr("n", typeof(int)) is not int n)
            throw new global::app.error.DeclinedException(new global::app.error.@this(
                "a saved event names no action that made it, or not its place among that action's events", "EventKeyIncomplete", 400));
        return context.Actor!.@event.Of(action).ElementAtOrDefault(n)
            ?? throw new global::app.error.DeclinedException(new global::app.error.@this(
                $"the saved event {n} made by '{action.Step?.Text}' is no event this actor holds", "EventNotFound", 404));
    }
}
