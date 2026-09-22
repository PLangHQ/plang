namespace app.snapshot;

/// <summary>
/// Typed read/write surface for a subsystem snapshot. A snapshot is a tree of
/// named sections; each section is itself a `@this` so a subsystem with nested
/// `ISnapshot` properties (e.g. App owning Variables, Errors, …) can give
/// each child its own subtree without leaking storage to the children.
///
/// The wire shape is the App tree — that's the OBP win the design hangs on.
/// Subsystems write entries via <see cref="Write{T}"/> and read via
/// <see cref="Read{T}"/>; the underlying storage is an implementation detail.
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly Dictionary<string, @this> _sections =
        new(StringComparer.OrdinalIgnoreCase);

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
        Entries = new global::app.type.item.dict.@this(context);
    }

    /// <summary>
    /// Returns the named subsection, creating it if missing. Subsystems hand the
    /// returned subtree to their nested ISnapshot children — each owns its scope.
    /// </summary>
    public @this Section(string name)
    {
        if (_sections.TryGetValue(name, out var existing)) return existing;
        var created = new @this(Context);
        _sections[name] = created;
        return created;
    }

    /// <summary>True if a section with this name was captured.</summary>
    public bool HasSection(string name) => _sections.ContainsKey(name);

    /// <summary>Names of all captured subsections, for App.Restore dispatch.</summary>
    public IReadOnlyCollection<string> SectionNames => _sections.Keys;

    /// <summary>Writes an entry, born as a plang value in this snapshot's context — the same entity
    /// door every other value birth goes through. Scalars lift (string→text, int→number,
    /// List&lt;snapshot&gt;→list); anything the door cannot make a value of fails HERE, at the site
    /// that wrote it, instead of surviving as a raw object for a serializer to guess at later.
    /// Overwrites any prior value at the same key.</summary>
    public void Write<T>(string key, T value)
        => Entries.Set(new global::app.data.@this(
            key, global::app.type.item.@this.Create(value, Context), context: Context));

    /// <summary>The nested sub-sections on this node, by name — read-only.</summary>
    public IReadOnlyDictionary<string, @this> Sections => _sections;

    /// <summary>The snapshot writes ITSELF: an object of its entries, then its sections. This node
    /// shape is the one structural thing the snapshot owns — below it, composition only, because
    /// each entry and each section writes itself.</summary>
    public override async System.Threading.Tasks.ValueTask Output(
        global::app.channel.serializer.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginObject();
        foreach (var entry in Entries.Entries)
        {
            writer.Name(entry.Name);
            await entry.Output(writer, mode, context);
        }
        foreach (var (name, section) in _sections)
        {
            writer.Name(name);
            await section.Output(writer, mode, context);
        }
        writer.EndObject();
    }
}
