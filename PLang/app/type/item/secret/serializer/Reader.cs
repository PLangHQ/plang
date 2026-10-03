namespace app.type.item.secret.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.type.item.secret.@this"/> — what
/// plang's store kept whole reads back as the secret.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
        => reader.Null() ? new global::app.type.item.@null.@this("secret", kind) : new global::app.type.item.secret.@this(reader.String());
}
