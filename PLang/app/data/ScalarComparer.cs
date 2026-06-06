using System;
using Number = global::app.type.number.@this;

namespace app.data;

/// <summary>
/// The thin scalar leaf — reached by <see cref="Compare"/> only for values that do
/// NOT own their own compare via <see cref="IEquatableValue"/> / <see cref="IOrderableValue"/>.
/// Post-born-native that is essentially <c>number</c> (which widens in its own tower
/// via <see cref="Number.CompareTo"/>) and any coerced raw string the mediator produced
/// (enum→name). Every other scalar flows as its wrapper and self-dispatches, so the
/// per-type arms (the old <c>Name()</c> switch, the <c>DateTime</c>/<c>DateOnly</c>/
/// <c>TimeSpan</c> arms) are gone — what remains is numeric + string + a thin
/// same-typed <see cref="IComparable"/> fallback for a raw scalar that slips through
/// a perimeter.
/// </summary>
internal static class ScalarComparer
{
    /// <summary>Equality on two coerced scalars (callers pass post-<c>NormalizeTypes</c> values).</summary>
    public static bool AreEqual(object? a, object? b)
    {
        // A wrapper may reach this leaf (e.g. one side raw, the other a wrapper, after
        // a not-yet-uniform construction). Unwrap to the raw backing so equality is by
        // value (raw bool == bool.@this), not a wrapper's reference Equals.
        if (a is global::app.type.item.@this ia) a = ia.ToRaw();
        if (b is global::app.type.item.@this ib) b = ib.ToRaw();
        if (a == null || b == null) return a == null && b == null;
        if (IsNumeric(a) && IsNumeric(b))
            return Number.FromObject(a)!.CompareTo(Number.FromObject(b)) == 0;
        if (a is string sa && b is string sb)
            return string.Equals(sa, sb, StringComparison.OrdinalIgnoreCase);
        return a.Equals(b);
    }

    /// <summary>
    /// Total order on two coerced scalars. Throws <see cref="Compare.NotOrderableException"/>
    /// for an equality-only type or two genuinely different value types — no invented
    /// cross-type order.
    /// </summary>
    public static int Order(object? a, object? b)
    {
        if (a is global::app.type.item.@this ia) a = ia.ToRaw();
        if (b is global::app.type.item.@this ib) b = ib.ToRaw();
        if (a == null || b == null)
            throw new Compare.NotOrderableException("cannot order a null scalar");

        if (IsNumeric(a) && IsNumeric(b))
            return Number.FromObject(a)!.CompareTo(Number.FromObject(b));
        if (a is string sa && b is string sb)
            return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);

        // Thin generic fallback: a same-typed raw scalar that slips through a perimeter
        // (a C# action returning a bare DateTimeOffset/TimeSpan) orders by its own
        // IComparable. `bool` is excluded — it is IComparable but equality-only (its
        // wrapper implements no IOrderableValue), so a raw bool must throw like one.
        if (a.GetType() == b.GetType() && a is not bool && a is IComparable ca)
            return ca.CompareTo(b);

        // No invented cross-type order; equality-only values (bool/null/dict) throw.
        throw new Compare.NotOrderableException(a.GetType() == b.GetType()
            ? $"cannot order {a.GetType().Name} — it is an equality-only type (no natural ordering)"
            : $"cannot order {a.GetType().Name} against {b.GetType().Name}");
    }

    // The numeric set number recognises — number.@this plus any raw CLR numeric that
    // slips through a perimeter, so the wrap-through-Number behavior is unchanged.
    private static bool IsNumeric(object v) =>
        v is Number || v is int or long or short or byte or float or double or decimal;
}
