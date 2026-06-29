namespace app.goal.call;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) reader for <c>goal.call</c> — a call
/// descriptor (<see cref="global::app.goal.GoalCall"/>, itself an <c>item.@this</c>). Its dotted
/// type name can't be derived from a <c>serializer</c> namespace, so this lives OUTSIDE
/// <c>*.serializer</c> (discovery skips it) and the reader registry registers it explicitly under
/// <c>goal.call</c> — no discovery fork. goal.call still rides STJ (its nested Data params
/// deserialize through the Wire), so the reader builds its read options from its own
/// <c>ReadContext</c>. Folding this into the streaming read is the goal.call follow-on.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var options = global::app.data.Wire.ReadOptions(ctx with { Verify = false });
        return (global::app.type.item.@this?)System.Text.Json.JsonSerializer
                   .Deserialize<global::app.goal.GoalCall>(reader.RawValue(), options)
               ?? new global::app.type.@null.@this("goal.call", kind);
    }
}
