namespace app.type.item.parallel.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.type.item.parallel.@this"/> — the
/// read-back mirror of its <c>Write</c>: <c>false</c> is off, <c>true</c> (what the build writes for <c>in parallel</c>)
/// and <c>{}</c> are parallel at the machine's default, <c>{cpu: n}</c> is n at once.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("parallel", kind);
        if (reader.Peek() == global::app.type.format.TokenKind.Bool)
            return reader.Bool() ? new global::app.type.item.parallel.@this() : global::app.type.item.parallel.@this.Off;
        long? cpu = null;
        reader.BeginObject();
        while (reader.NextName(out var name))
        {
            if (string.Equals(name, "cpu", System.StringComparison.OrdinalIgnoreCase)) cpu = reader.Long();
            else throw new System.FormatException($"parallel's one member is cpu (how many at once) — not {name}");
        }
        reader.EndObject();
        return new global::app.type.item.parallel.@this(cpu);
    }
}
