namespace app.snapshot.serializer;

/// <summary>
/// Typed (<see cref="app.type.reader.ITypeReader"/>) pull reader for <c>snapshot</c> — the read-side
/// mirror of <see cref="app.snapshot.@this.Output"/>.
///
/// <para>A snapshot has ONE structure: a dict of Data. So this reads one kind of member and asks
/// nothing about what kind it is — each entry comes back through the value slot, and an entry whose
/// value is a snapshot IS a subsection. The recursion is the registry's, not a walker's: a
/// snapshot-typed value dispatches straight back here.</para>
///
/// <para>Registered (parameterless), not born holding a parent: a section needs no parent as a birth
/// fact — it is a VALUE, reached by reading the entry that holds it.</para>
/// </summary>
public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    /// <summary>A section is structure, not content: <see cref="app.snapshot.@this.Section"/> and
    /// its sibling views navigate straight into it, so it must be a real snapshot when they look,
    /// not a slice waiting to be asked.</summary>
    public bool IsEager => true;

    /// <summary>A snapshot is an object of its entries (or null); a string in a slot names a variable holding one. Its own
    /// text (a file read, a db row) is read when it is asked as a snapshot (<see cref="Read{TReader}"/>).</summary>
    public bool Reads(global::app.type.format.TokenKind token)
        => token is global::app.type.format.TokenKind.Object or global::app.type.format.TokenKind.Null;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.type.format.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this(ctx.Context.App.type.list[new global::app.type.@this("snapshot", kind), ctx.Context]);

        // a snapshot's entries were written in the snapshot's view, so they are read in it: a value found elsewhere
        // comes back as what its key finds
        ctx = ctx with { View = global::app.View.Snapshot };

        // its text is its written form, plang's own: read back through here as the object it holds
        if (reader.Peek() == global::app.type.format.TokenKind.String)
        {
            var types = ctx.Context.App.type.list;
            var plang = types.Plang;
            return plang.Read(new global::app.type.item.wire.@this(reader.String(), types[typeof(global::app.snapshot.@this)], plang, ctx.View), ctx);
        }

        reader.BeginObject();
        var json = new global::app.type.item.serializer.json(ctx.Context);
        // Born with the READING context: a restored snapshot belongs to the actor restoring it,
        // not to whoever captured it.
        var snapshot = new global::app.snapshot.@this(ctx.Context);
        while (reader.NextName(out var name))
            snapshot.Entries.Set(name, json.Entry(ref reader, ctx));
        reader.EndObject();
        return snapshot;
    }
}
