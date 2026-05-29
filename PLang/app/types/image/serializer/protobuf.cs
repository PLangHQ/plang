namespace app.types.image.serializer;

/// <summary>
/// Protobuf-writer renderer for <see cref="app.types.image.@this"/> — raw
/// bytes. Stub presence proves the (type, format) table handles non-string
/// primitives; will become load-bearing when a protobuf writer ships.
/// </summary>
public static class protobuf
{
    public static void Write(global::app.types.image.@this value, global::app.channel.serializer.IWriter writer)
    {
        if (value == null) { writer.Null(); return; }
        writer.Bytes(value.Bytes);
    }
}
