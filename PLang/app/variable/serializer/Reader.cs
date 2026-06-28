namespace app.variable.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>variable</c> — a
/// name-slot (a write target, <c>type:variable</c>). The raw IS the variable name; the
/// reader pulls it and resolves the named variable (<see cref="app.variable.@this.Resolve"/>).
/// A variable names a thing, it is not a value parsed from bytes — so it resolves the name,
/// it does not coerce content.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
        => global::app.variable.@this.Resolve(reader.String(), ctx.Context);
}
