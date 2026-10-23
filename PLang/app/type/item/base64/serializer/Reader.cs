namespace app.type.item.base64.serializer;

public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        var type = ctx.Context.App.type.list[new global::app.type.@this("base64", kind), ctx.Context];
        if (reader.Null()) return new global::app.type.item.@null.@this(type);
        // Validation home: a slot DECLARED base64 must hold one. Parse's FormatException
        // rides source.Value's catch → MaterializeFailed, named to the binding. A data-url's own mime is its kind.
        return @this.Parse(reader.String(), type, ctx.Context);
    }
}
