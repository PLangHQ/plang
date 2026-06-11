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
/// answers <c>null</c> and lands the reason on <paramref name="asking"/> via
/// <c>asking.Fail</c> — the error always belonged to the binding the caller
/// already holds, never to a freshly minted one. Implementations touch ONLY
/// <c>asking.Fail</c>; everything else on the Data is courier state and
/// off-limits here.</para>
/// </summary>
public interface ICreate<TSelf> where TSelf : @this, ICreate<TSelf>
{
    static virtual TSelf? Create(@this value, global::app.data.@this asking)
    {
        // Pass-through (already TSelf) and the chain facet (a Data<file> slot
        // satisfied after the file parsed to dict) — free for every type, and
        // the same instance rides out: no new value, no new binding.
        if (value is TSelf self) return self;
        if (value.Facet<TSelf>() is { } facet) return facet;

        // The conversion body — the registry's per-type Convert dispatch
        // (text→number, text→choice/enum, text→datetime, json→dict, …): the
        // SINGLE construction home both the typed ask and the reader door call
        // (reader-path ruling). A type with a richer story overrides Create;
        // a decline lands its reason on the asking binding and answers null.
        var (converted, error) = global::app.type.catalog.@this.TryConvert(
            value, typeof(TSelf), asking.Context, asking.Name);
        if (error != null) { asking.Fail(error); return null; }
        if (converted is TSelf made) return made;

        asking.Fail(new global::app.error.Error(
            $"%{asking.Name}% holds a {value.Mint().Name} — '{@this.NameOf(typeof(TSelf))}' cannot be created from it.",
            "CreateDeclined", 400));
        return null;
    }
}
