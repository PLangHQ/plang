namespace app.snapshot;

/// <summary>
/// A plain container of plang values: its <see cref="Entries"/> are a dict of Data, and a part's node
/// is an entry whose value is itself a snapshot — one structure, so a reader walks it without asking
/// what kind of member it is looking at. Each <see cref="ISnapshot"/> part writes its own node and
/// reads it back through the entries' typed asks when it starts. The shape is the app's member tree.
/// </summary>
[global::app.Attributes.PlangType("snapshot")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
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

    /// <summary>A snapshot born in <paramref name="context"/>, holding the app's snapshot type — the name a reader
    /// dispatches on when a section comes back.</summary>
    public @this(global::app.actor.context.@this context) : base(context.App.type.list[typeof(@this)])
    {
        Context = context;
        Entries = new global::app.type.item.dict.@this(context.App.type.list[typeof(global::app.type.item.dict.@this)]);
    }

    /// <summary>The stored Data as <paramref name="context"/> holds it — the one door a part takes stored Data into live
    /// memory by: each entry cloned, so the run never shares a value with the snapshot, and born again in the context the
    /// part was handed, so it answers as the actor whose memory it goes into, never as whoever read the text. What is
    /// still unread (a value through text) opens in that context too.</summary>
    internal IEnumerable<global::app.data.@this> In(global::app.actor.context.@this context)
    {
        foreach (var entry in Entries.Entries(context)) yield return entry.Clone().Copy(context);
    }

    /// <summary>A snapshot that holds nothing is empty, as a collection is: the walk and <see cref="Write"/> leave it out.</summary>
    public override bool IsTruthy() => Entries.IsTruthy();

    /// <summary>A collection entry, written only when it holds something — the mirror of <see cref="Read{T}"/>, which
    /// reads an absent entry as empty. Empty is the one test the walk leaves an empty part out by.</summary>
    internal void Write(string key, global::app.type.item.@this collection)
    {
        if (collection.IsTruthy()) Entries.Set(new global::app.data.@this(key, collection, context: Context));
    }

    /// <summary>The rows a list entry holds, as <paramref name="context"/> holds them — the entry taken in through the
    /// same door as <see cref="In"/>, each row then read through its own typed ask, so nothing lowers to CLR. An absent
    /// entry is empty, the same answer a written empty list gives. A row that doesn't read is the answer, its own error
    /// whole; one that reads as nothing without saying why is answered as <c>SnapshotRowUnread</c>, naming the key.</summary>
    internal async System.Threading.Tasks.Task<global::app.data.@this<global::app.type.item.list.@this<T>>> Read<T>(string key,
        global::app.actor.context.@this context)
        where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
    {
        var rows = new global::app.type.item.list.@this<T>(context.App.type.list[typeof(global::app.type.item.list.@this<T>)]);
        if (Entries.Get(key, context)?.Clone().Copy(context) is { } entry)
        {
            if (await entry.Value<global::app.type.item.list.@this>() is not { } stored) return Unread(entry);
            foreach (var row in stored.Items(context))
            {
                if (await row.Value<T>() is not { } value) return Unread(row);
                rows.Add(value);
            }
        }
        return context.Ok(rows);

        global::app.data.@this<global::app.type.item.list.@this<T>> Unread(global::app.data.@this read)
            => global::app.data.@this<global::app.type.item.list.@this<T>>.FromError(read.Error ?? new global::app.error.@this(
                $"the snapshot's '{key}' holds a row that reads as no {(global::app.type.item.@this.NameOf(typeof(T)))}",
                "SnapshotRowUnread", 400), context);
    }

    /// <summary>The snapshot writes ITSELF: an object of its entries. One loop, because there is
    /// one kind of member — a subtree is an entry whose value is a snapshot, so it writes itself
    /// like every other value. This node shape is the only structural thing the snapshot owns;
    /// below it, composition.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        foreach (var entry in Entries.Entries(Context))
        {
            writer.Name(entry.Name);
            await entry.Output(writer);
        }
        writer.EndObject();
    }
}
