namespace app.type.item.base64.serializer;

public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("base64", kind);
        // Validation home: a slot DECLARED base64 must hold one. Parse's FormatException
        // rides source.Value's catch → MaterializeFailed, named to the binding.
        var b64 = @this.Parse(reader.String());
        return kind != null && b64.Kind == null ? b64.Kinded(kind) : b64;
    }
}
