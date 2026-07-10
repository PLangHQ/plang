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
public sealed class @this<T> : @this, global::app.type.item.ICreate<@this<T>>
    where T : global::app.type.item.@this
{
    public @this(global::app.actor.context.@this context) : base(context) { }
    public @this(System.Collections.Generic.IEnumerable<global::app.data.@this> items, global::app.actor.context.@this context) : base(items, context) { }
    public @this(System.Collections.Generic.IEnumerable<global::app.type.item.@this> values, global::app.actor.context.@this context) : base(values, context) { }

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
        return global::app.type.@this.Create((value as global::app.type.item.@this)?.Clr<object>() ?? value, data.Context) is @this lifted
            ? new @this<T>(lifted, data.Context!) : null;
    }

    /// <summary>
    /// The conversion catalog's owned hook (discovered via <c>convert.OwnerOf</c>, like
    /// <c>choice&lt;T&gt;</c>/<c>path</c>): build a <c>list&lt;T&gt;</c> from a raw sequence,
    /// converting each element to <typeparamref name="T"/>. The list owns its own construction
    /// (born-with-context via the value-sequence ctor); the catalog stays the per-element
    /// conversion authority (each element recurses through <c>TryConvert</c>, so its type owns
    /// how it's built). No reflection, no STJ round-trip.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind, global::app.actor.context.@this context)
    {
        if (value is not System.Collections.IEnumerable elements || value is string)
            return context.Error(new global::app.error.Error(
                $"Cannot convert {value?.GetType().Name ?? "null"} to list<{typeof(T).Name}> — expected a sequence.",
                "ListConversionFailed", 400));

        var values = new System.Collections.Generic.List<global::app.type.item.@this>();
        var errors = new System.Collections.Generic.List<global::app.error.Error>();
        int i = 0;
        foreach (var elem in elements)
        {
            var (converted, itemError) = global::app.type.catalog.@this.TryConvert(elem, typeof(T), context);
            if (itemError != null)
                errors.Add(new global::app.error.Error($"[{i}]: {itemError.Message}", "ElementConversionFailed", 400)
                    { FixSuggestion = itemError.FixSuggestion });
            else if (converted is global::app.type.item.@this iv)
                values.Add(iv);
            i++;
        }
        if (errors.Count > 0)
        {
            var err = new global::app.error.Error(
                $"Failed converting {errors.Count} elements to list<{typeof(T).Name}>",
                "ListConversionFailed", 400) { FixSuggestion = $"Element type: {typeof(T).Name}" };
            foreach (var e in errors) err.ErrorChain.Add(e);
            return context.Error(err);
        }
        return context.Ok(new @this<T>(values, context));
    }
}
