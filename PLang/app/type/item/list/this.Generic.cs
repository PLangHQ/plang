namespace app.type.item.list;

/// <summary>
/// Typed view of a native list — <c>list&lt;T&gt;</c>, where <c>T : item</c> is the element
/// type. A thin subclass of the (untyped) <see cref="@this"/>: it adds NO storage or behavior,
/// only the element-type tag so a slot can declare <c>Data&lt;list&lt;LlmMessage&gt;&gt;</c> and
/// the builder reads the element type intrinsically (no separate attribute). The element type
/// is the single source of truth — it lives in the type, not beside it.
///
/// Runtime instances are produced by the conversion catalog (As&lt;list&lt;T&gt;&gt;), which builds
/// a <c>list&lt;T&gt;</c> from raw and converts each element to T. Everything else — serializer,
/// navigators, comparison, <c>is app.type.item.list.@this</c> checks — sees the non-generic base.
/// </summary>
public class @this<T> : @this, global::app.type.item.ICreate<@this<T>>
    where T : global::app.type.item.@this, global::app.type.item.ICreate<T>
{
    public @this() { }

    /// <summary>A typed list is written with its element (<c>list&lt;goal&gt;</c>) however it was born; a list of
    /// any item (<c>list&lt;item&gt;</c>) is the plain list.</summary>
    protected internal override global::app.type.@this Type
        => base.Type is { kind.IsEmpty: false } kinded || typeof(T) == typeof(global::app.type.item.@this)
            ? base.Type
            : new(typeof(global::app.type.item.list.@this), global::app.type.item.@this.NameOf(typeof(T))) { Template = Template };

    public @this(System.Collections.Generic.IEnumerable<global::app.data.@this> items) : base(items) { }
    public @this(System.Collections.Generic.IEnumerable<global::app.type.item.@this> values) : base(values) { }

    /// <summary>The typed items — INTERNAL, the engine face a node subclass runs/walks (a
    /// <c>list&lt;action&gt;</c>'s <c>Run</c>, a <c>list&lt;step&gt;</c>'s sequence). NOT public:
    /// outside code tells the graph (run/wire/walk) or navigates it as values (the Data face,
    /// <c>Items(context)</c>), it never harvests the stored values. The read/parse seam stores
    /// the typed <typeparamref name="T"/> directly, so no context is needed to read them.</summary>
    internal System.Collections.Generic.IEnumerable<T> Items()
    {
        foreach (var slot in Slots()) yield return (T)(slot is global::app.data.@this d ? d.Peek() : slot)!;
    }

    /// <summary>The typed item at a flattened index — INTERNAL, the engine positional face
    /// (snapshot restore's <c>liveStep.Action[i]</c>, build validation's <c>goal.Step[i]</c>). Public
    /// index navigation still rides the Data-row face (<c>At</c>); this is the same-assembly typed
    /// door.</summary>
    internal T this[int index] => (T)(Slot(index)
        ?? throw new System.IndexOutOfRangeException($"index {index} is out of range for a list of {CountRaw}"));

    /// <summary>Program-node birth — a graph node (action.list / step.list) is
    /// shared across runs, so it stores no context. The elements ride as typed items in the backing.</summary>
    protected @this(System.Collections.Generic.List<object?> backing) : base(backing) { }

    /// <summary>Adopt another list's rows — the value→slot materialization (see the base overload).</summary>
    protected @this(global::app.type.item.list.@this source) : base(source) { }

    /// <summary>Render/clone preserve the element-type tag — a list&lt;T&gt; stays
    /// a list&lt;T&gt; instead of degrading to the non-generic base.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this<T>();

    private @this(System.Collections.Generic.List<object?> rows, bool wrapped) : base(rows, wrapped, null) { }

    /// <summary>A copy is a <c>list&lt;T&gt;</c> too; a program's node list (a subtype) is itself.</summary>
    private protected override global::app.type.item.list.@this Holding(System.Collections.Generic.List<object?> rows)
        => GetType() == typeof(@this<T>) ? new @this<T>(rows, wrapped: true) { Template = Template } : this;

    /// <summary>Value-membership typed to the element — because the parameter is
    /// <typeparamref name="T"/>, a caller can pass what converts to T (e.g. a bare
    /// <c>string</c> to a <c>list&lt;text&gt;</c>: <c>Contains("http")</c> lifts via
    /// <c>text</c>'s own <c>string</c> operator). Routes through the base membership.</summary>
    public async System.Threading.Tasks.ValueTask<global::app.type.item.@bool.@this> Contains(T value, global::app.actor.context.@this context)
        => await Contains(new global::app.data.@this("", value, context: context));

    /// <summary>A <c>list&lt;T&gt;</c> is a RE-TAG of a list, not an element walk: wrap the
    /// list's rows as-is. Each row converts to <typeparamref name="T"/> only when taken out
    /// (<c>row.Value&lt;T&gt;()</c>) — O(1) here, no per-element conversion.</summary>
    public static new @this<T>? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (value is @this<T> already) return already;
        if (value is @this list) return new @this<T>(list);
        // A raw container lifts to a base list first, then re-tags; one value is a list of one, as the list's reader
        // reads it.
        return global::app.type.item.@this.Create((value as global::app.type.item.@this)?.Clr<object>() ?? value, data.Context) switch
        {
            @this lifted => new @this<T>(lifted),
            { IsNull: false } one => new @this<T>(new[] { one }),
            _ => null,
        };
    }

    /// <summary>A typed list is made from any list: a list<typeparamref name="T"/> passes through, another is
    /// re-tagged.</summary>
    public static bool Takes(global::app.type.@this other) => other.Is("list");

    /// <summary>The members <paramref name="step"/> can name, as the decider is offered them — a collection that knows
    /// which of its members are reachable from a step answers them; any other offers none.</summary>
    internal virtual System.Threading.Tasks.ValueTask<System.Collections.Generic.IReadOnlyList<global::app.type.item.@this>> Offers(global::app.goal.step.@this step)
        => new([]);

    /// <summary>Walks the items, one at a time — the ones held. A list that loads its items (goal's) reads
    /// each as the walk reaches it, so a walk that stops early (the type's <c>Get(key)</c>) reads no
    /// further. With no asker: the app's own walk.</summary>
    internal virtual async System.Collections.Generic.IAsyncEnumerable<T> Walk()
    {
        foreach (var item in Items()) yield return item;
        await System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>Walks the items as <paramref name="context"/>'s actor asks for them; <paramref name="setting"/>
    /// as <see cref="all"/>. A list with no setting class walks the same for every asker.</summary>
    internal virtual System.Collections.Generic.IAsyncEnumerable<T> Walk(global::app.type.item.dict.@this? setting,
        global::app.actor.context.@this context) => Walk();

    /// <summary>Every item, as a list: the asker's walk gathered.</summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.list.@this> all(global::app.actor.context.@this context,
        global::app.type.item.dict.@this? setting = null)
    {
        var every = new @this<T>();
        await foreach (var item in Walk(setting, context)) every.Add(item);
        return every;
    }
}
