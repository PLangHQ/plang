namespace app.type.item.path.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for
/// <see cref="app.type.item.path.@this"/> — the inverse of <see cref="Default.Write"/>
/// (the portable <c>Relative</c> location string). The location pulls off the
/// single decode pass as a string token; with a Context it resolves scheme-correct
/// and fully wired via <see cref="app.type.item.path.@this.Resolve(string, actor.context.@this)"/>,
/// without one it borns a bare file-scheme stub (the Authorize callers then explode
/// on it — that is the contract).
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("path", kind);
        string s = reader.String();
        if (string.IsNullOrEmpty(s)) return new global::app.type.item.@null.@this("path", kind);
        // Born-with-context: the read context always carries the actor scope; a path resolves
        // through the scheme registry with it (no context-less stub).
        return global::app.type.item.path.@this.Resolve(s, ctx.Context);
    }
}
