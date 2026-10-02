namespace app.module.terminal.type.sandbox.serializer;

/// <summary>Typed pull reader for <see cref="app.module.terminal.type.sandbox.@this"/>: a sandbox writes itself as its
/// dict, so the value is pulled as a dict and the sandbox is made from it.</summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var nothing = new global::app.type.item.@null.@this("sandbox", kind);
        if (reader.Null()) return nothing;
        var written = global::app.type.item.@this.Create(new global::app.type.item.serializer.json(ctx.Context).Read(ref reader, ctx), ctx.Context);
        var carrier = new global::app.data.@this("", nothing, context: ctx.Context);
        // a sandbox that refuses what was written says why, at the read boundary
        return (global::app.type.item.@this?)global::app.module.terminal.type.sandbox.@this.Create(written, carrier.Type, carrier)
            ?? throw new global::app.error.DeclinedException(carrier.Error!);
    }
}
