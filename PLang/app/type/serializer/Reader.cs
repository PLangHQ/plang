namespace app.type.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>type</c> — a type
/// DESCRIPTOR. A <c>type</c> value does not describe content; it describes a plang type
/// (e.g. <c>number/long</c>), so it materializes to the type ENTITY itself
/// (<see cref="app.type.@this"/>, which IS an <c>item.@this</c>) — the consumer reads its
/// <c>.Name</c>/<c>.Kind</c>/<c>.ClrType</c> (e.g. <c>variable.set</c>'s <c>as</c> clause).
/// The entity's own <c>JsonConverter</c> owns the <c>{name, kind?, strict?}</c> wire shape;
/// the reader deserializes through it and stamps the reader's context.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        var entity = System.Text.Json.JsonSerializer.Deserialize<global::app.type.@this>(reader.RawValue());
        if (entity == null) return new global::app.type.@null.@this("type", kind);
        entity.Context = ctx.Context;
        return entity;
    }
}
