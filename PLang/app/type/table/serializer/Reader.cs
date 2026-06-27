namespace app.type.table.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for the <c>table</c>
/// shape encoded as <c>csv</c> — the kind names the encoding, so the wire value is
/// the csv text token. Pulls the string and parses it to a grid through the same
/// RFC-4180 decode the whole-payload <see cref="csv.Read"/> uses (header row + keyed
/// rows). Registered at <c>(table, csv)</c>.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => "csv";

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.@null.@this("table", kind);
        return (global::app.type.item.@this)(csv.Read(reader.String(), kind ?? "csv", ctx)
            ?? new global::app.type.@null.@this("table", kind));
    }
}
