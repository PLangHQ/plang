namespace app.@event.serializer;

/// <summary>
/// The event type's reader: an event is never written as a value — it is an item's, reached by its path
/// (<c>%!app.type.step.on.before%</c>, <c>%!app.module.file.read.on.start%</c>). A value written where an event goes
/// (<c>Event="/events/Runtime/X"</c>) is declined saying so — a refusal the build names, never a missing reader.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var written = reader.Peek() == global::app.type.format.TokenKind.String ? $"'{reader.String()}'" : "a value";
        throw new global::app.error.DeclinedException(new global::app.error.Error(
            $"an event is reached by its item's path — %!app.type.step.on.before% (each step, before it starts), " +
            $"%!app.type.goal.on.end% (each goal, after it ends) — not written as {written}", "NotAnEvent", 400));
    }
}
