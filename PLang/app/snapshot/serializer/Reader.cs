namespace app.snapshot.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>snapshot</c> — the read-side mirror of the
/// snapshot's write: an object holding its app, kept as the bytes the app wrote until a resume reads them back or a
/// navigation opens them.
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    /// <summary>A snapshot is read as it is met: what it holds stays the app's bytes, so nothing is gained by waiting.</summary>
    public bool IsEager => true;

    /// <summary>A snapshot is an object holding its app (or null). Its own text (a file read, a db row) is read when it
    /// is asked as a snapshot (<see cref="Read{TReader}"/>).</summary>
    public bool Reads(global::app.type.format.TokenKind token)
        => token is global::app.type.format.TokenKind.Object or global::app.type.format.TokenKind.Null;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this("snapshot", kind), ctx.Context]);

        // a snapshot's entries were written in the snapshot's view, so they are read in it: a value whose type chooses
        // what the view shows comes back from those members (a goal, as what its key finds)
        ctx = ctx with { View = global::app.View.Snapshot };

        // its text is its written form, plang's own: read back through here as the object it holds
        if (reader.Peek() == global::app.type.format.TokenKind.String)
        {
            var types = ctx.Context.App.type.list;
            var plang = types.Plang;
            return plang.Read(new global::app.type.item.wire.@this(reader.String(), types[typeof(global::app.snapshot.@this)], plang, ctx.View), ctx);
        }

        reader.BeginObject();
        var list = ctx.Context.App.type.list;
        global::app.type.item.@this? app = null;
        // what the app wrote stays as its bytes until it is read back (a resume) or opened (a navigation): written again
        // unopened, it is those bytes, as they were signed
        while (reader.NextName(out var name))
            if (string.Equals(name, "app", System.StringComparison.OrdinalIgnoreCase) && reader.Peek() == global::app.type.format.TokenKind.Object)
                app = new global::app.type.item.wire.@this(System.Text.Encoding.UTF8.GetString(reader.RawValue()), list[typeof(global::app.type.item.dict.@this)], list.Plang, ctx.View);
            else
                reader.Skip();
        reader.EndObject();
        // Born with the READING context: a restored snapshot belongs to the actor restoring it, not to whoever captured it.
        return new global::app.snapshot.@this(app, ctx.Context);
    }
}
