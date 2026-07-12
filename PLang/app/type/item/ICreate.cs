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
    /// The context-carrying lift — the entity-door/thunk entry, driven with the born-with context.
    /// The base delegates to the context-free core; only a type that RESOLVES against an actor
    /// (a reference fundamental — <c>path</c>/<c>file</c>/<c>image</c>/<c>url</c>) overrides to use
    /// <paramref name="ctx"/>. Context lives on the minority that needs it, not the scalar majority.
    /// </summary>
    static virtual TSelf? Create(object? raw, global::app.actor.context.@this? ctx)
        => TSelf.Create(raw);

    /// <summary>
    /// The courier — the typed ask (<c>Data.Value&lt;T&gt;()</c>): a decline lands its reason on
    /// <c>data.Fail</c> (the error belonged to the binding the caller already holds). A type with a
    /// kind override (number's <c>as decimal</c>) overrides this; the default runs the pure core,
    /// then the container deserialize, then fails typed.
    /// </summary>
    static virtual TSelf? Create(object? raw, global::app.data.@this data)
    {
        // Pass-through / chain facet — free for every type, same instance rides out.
        if (raw is TSelf self) return self;
        if (raw is @this fv && fv.Facet<TSelf>() is { } facet) return facet;

        // An error value isn't a convertible payload — keep it primary, demote the failure.
        if (raw is @this ev && ev.Clr<object>() is global::app.error.Error errVal)
        {
            errVal.ErrorChain.Add(new global::app.error.Error(
                $"%{data.Name}% holds an error — '{@this.NameOf(typeof(TSelf))}' cannot be created from it.",
                "TypeMismatch", 400));
            data.Fail(errVal);
            return null;
        }

        // The pure core builds it — the type owns its own arms (CLR/item coercions in one switch).
        if (TSelf.Create(raw, data.Context) is { } made) return made;

        // A dict/list deserializes ITSELF to a record / domain item (step, …). Only a
        // container reaches this — a genuine deserialize failure surfaces (it throws).
        if (raw is global::app.type.item.dict.@this or global::app.type.item.list.@this
            && ((@this)raw).Clr(typeof(TSelf)) is TSelf rec) return rec;

        data.Fail(new global::app.error.Error(
            $"%{data.Name}% holds a {(raw as @this)?.Type.Name ?? raw?.GetType().Name} — '{@this.NameOf(typeof(TSelf))}' cannot be created from it.",
            "CreateItemDeclined", 400));
        return null;
    }
}
