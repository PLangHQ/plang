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

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("snapshot", kind);

        reader.BeginObject();
        var parser = new global::app.type.item.serializer.json(ctx.Context);
        // Born with the READING context: a restored snapshot belongs to the actor restoring it,
        // not to whoever captured it.
        var snapshot = new global::app.snapshot.@this(ctx.Context);
        while (reader.NextName(out var name))
            snapshot.Entries.Set(name, parser.ReadSlot(ref reader, ctx));
        reader.EndObject();
        return snapshot;
    }
}
