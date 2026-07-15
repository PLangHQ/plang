namespace app.type.item.choice.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for ONE closed option set —
/// registered per (choice, kind) by the choice registry as it scans an assembly's closed sets
/// (<see cref="app.type.item.choice.list.@this.Register"/>). The wire form is the option's SYMBOL (a scalar, e.g.
/// <c>"=="</c>); the symbol parses through the type's own <see cref="app.type.item.choice.@this{T}.Parse"/>,
/// and an unknown symbol's <see cref="System.FormatException"/> rides the born path to
/// MaterializeFailed named to the binding. No reflection at read time — the closed type is
/// baked into <typeparamref name="T"/>.
/// </summary>
public sealed class Reader<T> : global::app.type.reader.ITypeReader where T : notnull
{
    private readonly string _kind;
    public Reader(string kind) { _kind = kind; }
    public string Kind => _kind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("choice", kind);
        return global::app.type.item.choice.@this<T>.Parse(reader.String());
    }
}
