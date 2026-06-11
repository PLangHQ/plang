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
/// overrides and falls back here; a decline carries the real reason on the
/// returned envelope's <c>Error</c>.</para>
///
/// <para><b>The guard line:</b> <paramref name="asking"/> is the binding the
/// answer is born into — implementations may touch ONLY
/// <c>asking.ShallowClone&lt;TSelf&gt;</c> and
/// <c>asking.CloneError&lt;TSelf&gt;</c> (a door implementation additionally
/// has <c>Fail</c>). Conversion happens inside a binding or not at all;
/// everything else on the Data is courier state and off-limits here.</para>
/// </summary>
public interface ICreate<TSelf> where TSelf : @this, ICreate<TSelf>
{
    static virtual global::app.data.@this<TSelf> Create(@this value, global::app.data.@this asking)
    {
        // Pass-through (already TSelf) and the chain facet (a Data<file> slot
        // satisfied after the file parsed to dict) — free for every type.
        if (value is TSelf self) return asking.ShallowClone<TSelf>(self);
        if (value.Facet<TSelf>() is { } facet) return asking.ShallowClone<TSelf>(facet);

        // The conversion body — the registry's per-type Convert dispatch
        // (text→number, text→choice/enum, text→datetime, json→dict, …): the
        // SINGLE construction home both the typed ask and the reader door call
        // (reader-path ruling). A type with a richer story overrides Create;
        // a decline carries the real reason on the returned envelope.
        var (converted, error) = global::app.type.catalog.@this.TryConvert(
            value, typeof(TSelf), asking.Context, asking.Name);
        if (error != null) { asking.Fail(error); return asking.CloneError<TSelf>(value); }
        return converted is TSelf made
            ? asking.ShallowClone<TSelf>(made)
            : asking.CloneError<TSelf>(value);
    }
}
