namespace app.type.item.clipboard.serializer;

/// <summary>Typed pull reader for <see cref="app.type.item.clipboard.@this"/>: <c>{"clipboard": …}</c> — a string is a
/// copied text, anything else a value read as itself. An object without it is refused, never guessed.</summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("clipboard", kind);
        reader.BeginObject();
        global::app.type.item.@this? content = null;
        while (reader.NextName(out var member))
        {
            if (member != "clipboard") { reader.Skip(); continue; }
            content = reader.Peek() == global::app.type.format.TokenKind.String
                ? (global::app.type.item.text.@this)reader.String()
                : global::app.type.item.@this.Create(new global::app.type.item.serializer.json(ctx.Context).Read(ref reader, ctx), ctx.Context);
        }
        reader.EndObject();
        return new global::app.type.item.clipboard.@this(content ?? throw new FormatException("a clipboard line holds what was copied: {\"clipboard\": …}"));
    }
}
