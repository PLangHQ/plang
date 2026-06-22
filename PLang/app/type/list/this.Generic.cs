namespace app.type.list;

/// <summary>
/// Typed view of a native list — <c>list&lt;T&gt;</c>, where <c>T : item</c> is the element
/// type. A thin subclass of the (untyped) <see cref="@this"/>: it adds NO storage or behavior,
/// only the element-type tag so a slot can declare <c>Data&lt;list&lt;LlmMessage&gt;&gt;</c> and
/// the builder reads the element type intrinsically (no separate attribute). The element type
/// is the single source of truth — it lives in the type, not beside it.
///
/// Runtime instances are produced by the conversion catalog (As&lt;list&lt;T&gt;&gt;), which builds
/// a <c>list&lt;T&gt;</c> from raw and converts each element to T. Everything else — serializer,
/// navigators, comparison, <c>is app.type.list.@this</c> checks — sees the non-generic base.
/// </summary>
public sealed class @this<T> : @this, global::app.type.item.ICreate<@this<T>>
    where T : global::app.type.item.@this
{
    public @this() : base() { }
    public @this(System.Collections.Generic.IEnumerable<global::app.data.@this> items) : base(items) { }

    /// <summary>Render/clone preserve the element-type tag — a list&lt;T&gt; stays
    /// a list&lt;T&gt; instead of degrading to the non-generic base.</summary>
    protected override global::app.type.list.@this Empty() => new @this<T>();

    /// <summary>A <c>list&lt;T&gt;</c> is a RE-TAG of a list, not an element walk: wrap the
    /// list's rows as-is. Each row converts to <typeparamref name="T"/> only when taken out
    /// (<c>row.Value&lt;T&gt;()</c>) — O(1) here, no per-element conversion.</summary>
    public static new @this<T>? Create(global::app.type.item.@this value, global::app.data.@this asking)
    {
        if (value is @this<T> already) return already;
        if (value is @this list) return new @this<T>(list) { Context = asking.Context! };
        // A single value / raw container lifts to a base list first, then re-tags.
        return global::app.type.@this.Create(value.Clr<object>(), asking.Context) is @this lifted
            ? new @this<T>(lifted) { Context = asking.Context! } : null;
    }

    /// <summary>Build a typed list from raw element values — each wrapped in a Data row.</summary>
    public static @this<T> Of(System.Collections.IEnumerable items)
    {
        var l = new @this<T>();
        foreach (var i in items)
            l.Add(i is global::app.data.@this d ? d : new global::app.data.@this("", i));
        return l;
    }
}
