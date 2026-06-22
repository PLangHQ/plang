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

        // The TARGET builds itself: a scalar via its own Convert hook (number/text/
        // date/choice/…), a container via the lift (raw dict/list → dict/list). No
        // central switch — the type owns its construction. (list<T> overrides with a
        // re-tag; record/typed-list creation is the owner's, not a hub's.)
        object? raw = value.Clr<object>();
        // An error value isn't a convertible payload — keep it primary, demote the
        // conversion failure onto its chain.
        if (raw is global::app.error.Error errVal)
        {
            errVal.ErrorChain.Add(new global::app.error.Error(
                $"%{asking.Name}% holds an error — '{@this.NameOf(typeof(TSelf))}' cannot be created from it.",
                "TypeMismatch", 400));
            asking.Fail(errVal);
            return null;
        }
        var owned = global::app.type.convert.@this.OfStatic(typeof(TSelf), raw, asking.Type?.Kind, asking.Context);
        // The owning hook ran and failed — surface ITS reason, not a generic decline.
        if (owned is { Success: false } && owned.Error is { } hookErr) { asking.Fail(hookErr); return null; }
        var built = owned?.Peek() ?? global::app.type.@this.Create(raw, asking.Context);
        if (built is TSelf made) return made;

        // A dict/list deserializes ITSELF to a record / domain item (step, …).
        try { if (value.Clr(typeof(TSelf)) is TSelf rec) return rec; }
        catch (System.Exception ex) when (ex is System.InvalidCastException or System.Text.Json.JsonException
                                           or System.NotSupportedException or System.FormatException) { }

        asking.Fail(new global::app.error.Error(
            $"%{asking.Name}% holds a {value.Mint().Name} — '{@this.NameOf(typeof(TSelf))}' cannot be created from it.",
            "CreateDeclined", 400));
        return null;
    }
}
