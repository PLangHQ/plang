namespace app.type.@bool.serializer;

using TokenKind = global::app.channel.serializer.TokenKind;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.@bool.@this"/> — the type reads its own value off the
/// single decode pass. Mirror of <see cref="Default.Read"/> (the object-raw
/// reader it supersedes for the wire structural read): a <c>bool</c> token
/// becomes the wrapper directly; a string form parses; a null token is a typed
/// absence. Format-agnostic — the same impl serves any <c>IReader</c> front-end.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("bool", kind);
        return reader.Peek() switch
        {
            TokenKind.Bool => new global::app.type.@bool.@this(reader.Bool()),
            TokenKind.String when bool.TryParse(reader.String(), out var parsed)
                => new global::app.type.@bool.@this(parsed),
            _ => new global::app.type.@null.@this("bool", kind),
        };
    }
}
