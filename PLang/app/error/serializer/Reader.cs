namespace app.error.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <see cref="app.error.Error"/> — the
/// read-back mirror of <see cref="app.error.Error.Write"/> (<c>{id, message, key, status, createdUtc,
/// fixSuggestion?, helpfulLinks?, list?}</c>), each causing error read back the same way.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    /// <summary>An error is structure, read where it is found — a received failure is known at once.</summary>
    public bool IsEager => true;

    /// <summary>An error is an object (or null); a string there names a variable holding one.</summary>
    public bool Reads(global::app.type.format.TokenKind token)
        => token is global::app.type.format.TokenKind.Object or global::app.type.format.TokenKind.Null;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("error", kind);
        reader.BeginObject();
        string id = "", message = "", key = "Error";
        global::app.type.item.status.@this status = 400;
        var createdUtc = System.DateTime.UtcNow;
        string? fixSuggestion = null, helpfulLinks = null;
        var causes = new System.Collections.Generic.List<global::app.error.Error>();
        while (reader.NextName(out var name))
        {
            switch (name.ToLowerInvariant())
            {
                case "id":            id = reader.String(); break;
                case "message":       message = reader.String(); break;
                case "key":           key = reader.String(); break;
                case "status":
                    if (new global::app.type.item.status.serializer.Reader().Read(ref reader, null, ctx) is global::app.type.item.status.@this read)
                        status = read;
                    break;
                case "createdutc":    createdUtc = reader.DateTime(); break;
                case "fixsuggestion": fixSuggestion = reader.String(); break;
                case "helpfullinks":  helpfulLinks = reader.String(); break;
                case "list":
                    reader.BeginArray();
                    while (reader.NextElement())
                        if (Read(ref reader, kind, ctx) is global::app.error.Error cause) causes.Add(cause);
                    reader.EndArray();
                    break;
                default: reader.Skip(); break;
            }
        }
        reader.EndObject();
        return global::app.error.Error.Restore(id, message, key, status, createdUtc, fixSuggestion, helpfulLinks, causes);
    }
}
