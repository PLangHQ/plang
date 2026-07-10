namespace app.type.item.permission.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.permission.@this"/> — the read-back mirror of
/// <see cref="app.type.item.permission.@this.Write"/> (<c>{actor, path, match, verbs:[…]}</c>).
/// Reconstructs the typed grant so a stored permission reads back as itself, not a generic
/// property-bag (whose view-blind output would diverge from the signed hash).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("permission", kind);
        reader.BeginObject();
        string actor = "", path = "";
        var match = global::app.type.item.permission.Match.Exact;
        var verbs = new System.Collections.Generic.HashSet<global::app.type.item.permission.Verb>();
        while (reader.NextName(out var name))
        {
            switch (name.ToLowerInvariant())
            {
                case "actor": actor = reader.String(); break;
                case "path":  path = reader.String(); break;
                case "match":
                    if (System.Enum.TryParse<global::app.type.item.permission.Match>(reader.String(), ignoreCase: true, out var m)) match = m;
                    break;
                case "verbs":
                    reader.BeginArray();
                    while (reader.NextElement())
                        if (System.Enum.TryParse<global::app.type.item.permission.Verb>(reader.String(), ignoreCase: true, out var v)) verbs.Add(v);
                    reader.EndArray();
                    break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
        return new global::app.type.item.permission.@this(actor, path, verbs, match);
    }
}
