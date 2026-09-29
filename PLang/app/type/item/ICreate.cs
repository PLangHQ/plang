namespace app.type.item;

/// <summary>
/// The named constructor of every askable type — "construct yourself from
/// this value, or decline." The typed ask (<c>Data.Value&lt;T&gt;()</c>)
/// dispatches here at compile time: each type implements ITS OWN
/// <see cref="Create"/>; adding a type is adding a class; nothing central
/// exists. This is <c>new T(item)</c> made generically callable and able to
/// decline — the TARGET owns the conversion ("we want number, the number
/// knows how to create it"), never the source, never a catalog above the
/// types.
///
/// <para>The default answers what EVERY type gets for free: pass-through
/// (the value already is a <typeparamref name="TSelf"/>) and the chain facet
/// (the value evolved FROM one — a <c>Data&lt;file&gt;</c> slot stays
/// satisfied after the file parsed to dict). A type with real conversions
/// overrides and falls back here.</para>
///
/// <para><b>Returns the instance, not a binding.</b> Create answers the
/// <typeparamref name="TSelf"/> value itself — pass-through returns the very
/// same instance (zero allocation; the value already is one). A decline
/// answers <c>null</c> and lands the reason on <paramref name="data"/> via
/// <c>data.Fail</c> — the error always belonged to the binding the caller
/// already holds, never to a freshly minted one. Implementations touch ONLY
/// <c>data.Fail</c>; everything else on the Data is courier state and
/// off-limits here.</para>
/// </summary>
public interface ICreate<TSelf> where TSelf : @this, ICreate<TSelf>
{
    /// <summary>
    /// The pure core — the ONE runtime boundary: born-native <paramref name="raw"/> into a
    /// <typeparamref name="TSelf"/>, or decline (null). <c>object</c> because this method IS the
    /// crossing — a raw CLR value and an item of another type flow through the SAME switch
    /// (<c>int i => …</c> beside <c>text t => …</c>); no <c>Clr</c> shuttle wrapping a scalar just
    /// to open it a frame later. CONTEXT-FREE — the caller most often is coercion (`text → number`
    /// in compare) or a scalar lift, neither of which resolves against an actor. No Fail — an
    /// un-liftable value is not a failure (unowned falls to the caller's <c>Clr</c>). The base
    /// answers pass-through; each owning type overrides with its arms.
    /// </summary>
    static virtual TSelf? Create(object? raw)
        => raw as TSelf;

    /// <summary>
    /// The birth — the one door a declared value is made through (the typed ask <c>Data.Value&lt;T&gt;()</c>,
    /// the type door): <paramref name="raw"/> made a <typeparamref name="TSelf"/> as <paramref name="declared"/>
    /// says (its facts: kind, template), for the binding <paramref name="data"/> — which names it, carries the
    /// context, and takes the reason of a decline (<c>data.Fail</c>). A null <paramref name="declared"/> is "as
    /// the binding declares" (<c>data.Type</c>, asked only by a type that needs the declaration — so the typed
    /// ask pays nothing for it). A type that needs none of the declaration ignores it; a type that resolves
    /// against an actor (<c>path</c>/<c>file</c>/<c>url</c>) reads <c>data.Context</c>. The default runs the
    /// pure core, then the container deserialize, then fails typed.
    /// </summary>
    static virtual TSelf? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        // Pass-through — the same instance rides out.
        if (raw is TSelf self) return self;

        // The pure core builds it — the type owns its own arms (CLR/item coercions in one switch).
        if (TSelf.Create(raw) is { } made) return made;

        // A dict/list deserializes ITSELF to a record / domain item (step, …). Only a
        // container reaches this — a genuine deserialize failure surfaces (it throws).
        if (raw is global::app.type.item.dict.@this or global::app.type.item.list.@this
            && ((@this)raw).Clr(typeof(TSelf)) is TSelf rec) return rec;

        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {(raw as @this)?.Type.Name ?? raw?.GetType().Name} — '{@this.NameOf(typeof(TSelf))}' cannot be created from it.",
            "CreateItemDeclined", 400));
        return null;
    }

    /// <summary>
    /// Is a value of <paramref name="other"/>, declared a <typeparamref name="TSelf"/>, made into one — asked
    /// without making. A file, url or directory is made from a path (the reference to what is there), a typed
    /// list from a list; a container of any other type declared this one is held as it is. The base takes none.
    /// </summary>
    static virtual bool Takes(global::app.type.@this other) => false;

    /// <summary>
    /// A value of this type is a name (<see cref="global::app.type.item.variable.IName"/>): born from its text at
    /// once — never deferred as content — and never opened for a value, since it names one.
    /// </summary>
    static virtual bool IsName => typeof(global::app.type.item.variable.IName).IsAssignableFrom(typeof(TSelf));
}
