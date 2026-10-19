namespace app.module.output.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.module.output.Ask"/> — the
/// read-back mirror of what an ask writes, <c>{question}</c>: an ask that arrived from another plang (one speaking
/// plang's own format) reads as an Ask with its question, for the reader to answer. Read back, it is never waiting:
/// the reader's goal doesn't stop on it.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this("ask", kind), ctx.Context]);
        reader.BeginObject();
        global::app.type.item.text.@this? question = null;
        while (reader.NextName(out var name))
        {
            if (string.Equals(name, "question", System.StringComparison.OrdinalIgnoreCase) && !reader.Null())
                question = new global::app.type.item.text.@this(reader.String(), ctx.Context.App.type.list[typeof(global::app.type.item.text.@this)]);
            else reader.Skip();
        }
        reader.EndObject();
        return new global::app.module.output.Ask { Question = question };
    }
}
