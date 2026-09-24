namespace app.snapshot;

/// <summary>
/// A plain container of plang values: its <see cref="Entries"/> are a dict of Data, and a section is
/// an entry whose value is itself a snapshot. Each <see cref="ISnapshot"/> owner writes its own
/// section (<see cref="Write{T}"/>, or an entry set directly) and reads it back through the entries'
/// typed asks on restore. The wire shape is the App tree.
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The snapshot names its own type. A wire must not depend on a reflection-derived
    /// name — the name is what a reader dispatches on when a section comes back.</summary>
    protected internal override global::app.type.@this Type => new("snapshot", typeof(@this));

    /// <summary>
    /// The actor context this snapshot tree is born in. The snapshot renders its entries
    /// as self-describing Data (its leaf serializer builds them), so it must carry the
    /// context to born them — born-with-context, never construct-then-stamp. Sub-sections
    /// inherit it. Read-back (<see cref="Create"/>) borns in the data binding's context.
    /// </summary>
    public global::app.actor.context.@this Context { get; internal set; }

    /// <summary>The entries on this node as plang VALUES — the node they are, not a raw bag.
    /// An <c>object?</c> bag forces a central type-switch downstream to decide what each entry is;
    /// that switch was this snapshot's leaf-serializer, and its reflection fallback walked whatever
    /// it was handed, including live graph nodes with back-references. Typed here at the write door,
    /// every entry writes itself and there is nothing for a switch to do. Restore reads through the
    /// entry's own typed ask.</summary>
    public global::app.type.item.dict.@this Entries { get; }

    public @this(global::app.actor.context.@this context)
    {
        Context = context;
        Entries = new global::app.type.item.dict.@this();
    }

    /// <summary>
    /// Returns the named subsection, creating it if missing. Subsystems hand the
    /// returned subtree to their nested ISnapshot children — each owns its scope.
    /// <para>A section is an ENTRY whose value is a snapshot. It is not a second kind of thing
    /// stored in a second place: the snapshot has one structure, a dict of Data, and a subtree is a
    /// value in it like any other. That is what lets a reader walk the wire without asking what
    /// kind of member it is looking at.</para>
    /// </summary>
    public @this Section(string name)
    {
        if (Entries.Get(name, Context)?.Peek() is @this existing) return existing;
        var created = new @this(Context);
        Entries.Set(new global::app.data.@this(name, created, context: Context));
        return created;
    }

    /// <summary>True if a section with this name was captured.</summary>
    public bool HasSection(string name) => Entries.Get(name, Context)?.Peek() is @this;

    /// <summary>Names of all captured subsections, for App.Restore dispatch.</summary>
    public IReadOnlyCollection<string> SectionNames
        => Entries.Entries(Context).Where(e => e.Peek() is @this).Select(e => e.Name).ToList();

    /// <summary>The nested sub-sections on this node, by name — the entries that hold one.</summary>
    public IReadOnlyDictionary<string, @this> Sections
        => Entries.Entries(Context).Where(e => e.Peek() is @this)
                  .ToDictionary(e => e.Name, e => (@this)e.Peek(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Writes an entry, born as a plang value in this snapshot's context — the same entity
    /// door every other value birth goes through. Scalars lift (string→text, int→number,
    /// List&lt;snapshot&gt;→list); anything the door cannot make a value of fails HERE, at the site
    /// that wrote it, instead of surviving as a raw object for a serializer to guess at later.
    /// Overwrites any prior value at the same key.</summary>
    public void Write<T>(string key, T value)
        => Entries.Set(new global::app.data.@this(
            key, global::app.type.item.@this.Create(value, Context), context: Context));

    /// <summary>The snapshot writes ITSELF: an object of its entries. One loop, because there is
    /// one kind of member — a subtree is an entry whose value is a snapshot, so it writes itself
    /// like every other value. This node shape is the only structural thing the snapshot owns;
    /// below it, composition.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        foreach (var entry in Entries.Entries(Context))
        {
            writer.Name(entry.Name);
            await entry.Output(writer, mode, context);
        }
        writer.EndObject();
    }
}
