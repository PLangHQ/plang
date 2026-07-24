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
    where T : global::app.type.item.@this
{
    public @this(global::app.actor.context.@this context) : base(context) { }
    public @this(System.Collections.Generic.IEnumerable<global::app.data.@this> items, global::app.actor.context.@this context) : base(items, context) { }
    public @this(System.Collections.Generic.IEnumerable<global::app.type.item.@this> values, global::app.actor.context.@this context) : base(values, context) { }

    /// <summary>The typed elements — INTERNAL, the engine face a node subclass runs/walks (a
    /// <c>list&lt;action&gt;</c>'s <c>Run</c>, a <c>list&lt;step&gt;</c>'s sequence). NOT public:
    /// outside code tells the graph (run/wire/walk) or navigates it as values (the Data-row face),
    /// it never harvests raw elements. Each row Peeks its stored item (the read/parse seam stores
    /// the typed <typeparamref name="T"/> directly).</summary>
    internal System.Collections.Generic.IReadOnlyList<T> Elements
    {
        get
        {
            var built = new System.Collections.Generic.List<T>(CountRaw);
            foreach (var row in Items) built.Add((T)row.Peek());
            return built;
        }
    }

    /// <summary>The typed element at a flattened index — INTERNAL, the engine positional face
    /// (snapshot restore's <c>liveStep.Action[i]</c>, build validation's <c>goal.Step[i]</c>). Public
    /// index navigation still rides the Data-row face (<c>At</c>); this is the same-assembly typed
    /// door.</summary>
    internal T this[int index] => (T)(At(index)?.Peek()
        ?? throw new System.IndexOutOfRangeException($"index {index} is out of range for a list of {CountRaw}"));

    /// <summary>Program-node birth — a graph node (action.list / step.list / parameter.list) is
    /// shared across runs, so it stores no context. The elements ride as typed items in the backing.</summary>
    protected @this(System.Collections.Generic.List<object?> backing) : base(backing) { }

    /// <summary>Render/clone preserve the element-type tag — a list&lt;T&gt; stays
    /// a list&lt;T&gt; instead of degrading to the non-generic base.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this<T>(Context);

    /// <summary>Value-membership typed to the element — because the parameter is
    /// <typeparamref name="T"/>, a caller can pass what converts to T (e.g. a bare
    /// <c>string</c> to a <c>list&lt;text&gt;</c>: <c>Contains("http")</c> lifts via
    /// <c>text</c>'s own <c>string</c> operator). Routes through the base membership.</summary>
    public System.Threading.Tasks.ValueTask<global::app.type.item.@bool.@this> Contains(T value)
        => Contains((global::app.type.item.@this)value);

    /// <summary>A <c>list&lt;T&gt;</c> is a RE-TAG of a list, not an element walk: wrap the
    /// list's rows as-is. Each row converts to <typeparamref name="T"/> only when taken out
    /// (<c>row.Value&lt;T&gt;()</c>) — O(1) here, no per-element conversion.</summary>
    public static new @this<T>? Create(object? value, global::app.data.@this data)
    {
        if (value is @this<T> already) return already;
        if (value is @this list) return new @this<T>(list, data.Context!);
        // A single value / raw container lifts to a base list first, then re-tags.
        return global::app.type.item.@this.Create((value as global::app.type.item.@this)?.Clr<object>() ?? value, data.Context) is @this lifted
            ? new @this<T>(lifted, data.Context!) : null;
    }

}
