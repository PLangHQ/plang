namespace app.goal.call;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) reader for <c>goal.call</c> — a call
/// descriptor (<see cref="global::app.goal.GoalCall"/>, itself an <c>item.@this</c>). Its dotted
/// type name can't be derived from a <c>serializer</c> namespace, so this lives OUTSIDE
/// <c>*.serializer</c> (discovery skips it) and the reader registry registers it explicitly under
/// <c>goal.call</c> — no discovery fork. A GoalCall is a host shape, so it reads through the
/// reflection kind (its <c>[Store]</c> face: <c>Name</c>, <c>Parallel</c>, <c>Parameters</c>,
/// <c>PrPath</c>) — the same reader that reads every tagged host, routing the <c>List&lt;Data&gt;</c>
/// params through the <c>@schema:data</c> reader sign-identically. No STJ.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => (global::app.type.item.@this?)new global::app.type.item.kind.reflection.@this()
               .Read(ref reader, typeof(global::app.goal.GoalCall), ctx with { Verify = false })
           ?? new global::app.type.item.@null.@this("goal.call", kind);
}
