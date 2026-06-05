using Number = global::app.type.number.@this;

namespace app.data;

/// <summary>
/// The one legal type-switch over raw C# scalars — the leaf that compares two
/// already-coerced scalar values (number, text/string, datetime, duration, bool).
/// <c>Compare</c> (the mediator) dispatches here once a value is known not to own
/// its own compare via <see cref="IEquatableValue"/> / <see cref="IOrderableValue"/>.
///
/// <para>Equality and order both live here, so a scalar answers the two questions
/// the <em>same</em> way — number routes both through the number tower
/// (<c>Number.CompareTo</c>), killing the old split where order used the tower but
/// equality flattened to a boxed primitive. The arms are by raw CLR type because
/// scalars don't yet flow as their wrappers; the <c>scalars-as-native</c> branch
/// relocates these arms onto the wrappers and this leaf shrinks to the generic
/// <c>IComparable</c>/<c>IEquatable</c> fallthrough.</para>
/// </summary>
internal static class ScalarComparer
{
    /// <summary>Equality on two coerced scalars (callers pass post-<c>NormalizeTypes</c> values).</summary>
    public static bool AreEqual(object? a, object? b)
    {
        if (a == null || b == null) return a == null && b == null;
        if (IsNumeric(a) && IsNumeric(b))
            return Number.FromObject(a)!.CompareTo(Number.FromObject(b)) == 0;
        if (a is string sa && b is string sb)
            return string.Equals(sa, sb, System.StringComparison.OrdinalIgnoreCase);
        return a.Equals(b);
    }

    /// <summary>
    /// Total order on two coerced scalars. Throws <see cref="Compare.NotOrderableException"/>
    /// for a non-orderable value (an equality-only type) or two genuinely different
    /// value types — no invented cross-type order.
    /// </summary>
    public static int Order(object? a, object? b)
    {
        if (a == null || b == null)
            throw new Compare.NotOrderableException("cannot order a null scalar");

        if (IsNumeric(a) && IsNumeric(b))
            return Number.FromObject(a)!.CompareTo(Number.FromObject(b));
        if (a is string sa && b is string sb)
            return string.Compare(sa, sb, System.StringComparison.OrdinalIgnoreCase);
        if (a is System.TimeSpan ta && b is System.TimeSpan tb)
            return ta.CompareTo(tb);
        if (IsDateTime(a) && IsDateTime(b))
            return ToOffset(a).CompareTo(ToOffset(b));

        string na = Name(a), nb = Name(b);
        throw new Compare.NotOrderableException(na == nb
            ? $"cannot order {na} — it is an equality-only type (no natural ordering)"
            : $"cannot order {na} against {nb}");
    }

    // The numeric set number recognises — matches the old Family "number" arm plus
    // number.@this, so the wrap-through-Number behavior is unchanged.
    private static bool IsNumeric(object v) =>
        v is Number || v is int or long or short or byte or float or double or decimal;

    private static bool IsDateTime(object v) =>
        v is System.DateTime or System.DateTimeOffset or System.DateOnly;

    private static System.DateTimeOffset ToOffset(object v) => v switch
    {
        System.DateTimeOffset dto => dto,
        System.DateTime dt => new System.DateTimeOffset(dt),
        System.DateOnly d => new System.DateTimeOffset(d.ToDateTime(System.TimeOnly.MinValue)),
        _ => throw new Compare.NotOrderableException($"cannot order datetime value of CLR type {v.GetType().Name}"),
    };

    // Error-message only — a readable PLang-ish name, never used to pick behavior.
    private static string Name(object? v) => v switch
    {
        null => "null",
        bool => "bool",
        string => "text",
        dict => "dict",
        app.type.list.@this => "list",
        System.TimeSpan => "duration",
        System.DateTime or System.DateTimeOffset or System.DateOnly => "datetime",
        _ when IsNumeric(v) => "number",
        _ => v.GetType().Name.ToLowerInvariant(),
    };
}
