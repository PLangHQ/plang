namespace app.data;

/// <summary>
/// The single typed-compare path — the mediator. Both the condition operators
/// (<c>&gt;</c>, <c>&lt;</c>, <c>==</c> via <c>Operator</c>) and <c>list.sort</c>
/// route through here, so <c>if a.age &gt; b.age</c> and <c>sort by "age"</c> can
/// never drift.
///
/// <para>The mediator owns only three things: null policy (both null = equal; null
/// sorts last), coercion (<see cref="app.module.condition.Operator.NormalizeTypes"/> —
/// numeric widening, string↔number), and dispatch. It never asks <em>what a value
/// is</em> to pick behavior: a value that owns its compare implements
/// <see cref="IEquatableValue"/> / <see cref="IOrderableValue"/> (the collections);
/// everything else is a scalar handled by <see cref="ScalarComparer"/> (the one
/// legal type-switch). Orderable = implements <c>IOrderableValue</c> or the scalar
/// comparer accepts it; equality-only types (<c>dict</c>) simply don't implement
/// <c>IOrderableValue</c>, so <see cref="Order"/> throws for them.</para>
/// </summary>
public static class Compare
{
    /// <summary>
    /// Raised when two values cannot be ordered (different value types, or an
    /// equality-only type). Derives from ArgumentException so the condition
    /// evaluator's catch filter surfaces it as a clean EvaluationError.
    /// </summary>
    public sealed class NotOrderableException(string message) : System.ArgumentException(message);

    /// <summary>
    /// Total order on two element values. Nulls sort last; a value owning its order
    /// (<see cref="IOrderableValue"/>) decides for itself; otherwise the coerced
    /// scalars route through <see cref="ScalarComparer"/>. A different value type —
    /// or an equality-only type — throws <see cref="NotOrderableException"/>.
    /// </summary>
    public static int Order(@this? left, @this? right)
    {
        object? lv = left?.Materialize();
        object? rv = right?.Materialize();

        // A present null value rides as the null.@this singleton; the sort-last
        // policy lives here on Compare (the wrapper deliberately implements no
        // IOrderableValue), so coalesce the singleton to a C# null for the policy.
        if (lv is app.type.@null.@this) lv = null;
        if (rv is app.type.@null.@this) rv = null;

        // Nulls sort last: null is the greatest element.
        if (lv == null && rv == null) return 0;
        if (lv == null) return 1;
        if (rv == null) return -1;

        // Coerce cross-type FIRST (text "5" vs number 5) so a value's own order runs
        // on a reconciled pair. Same-type pairs pass through unchanged.
        (lv, rv) = app.module.condition.Operator.NormalizeTypes(lv, rv);

        // The value owns its order (lists: lexicographic; the scalar wrappers that
        // honor ordering). It recurses back through this mediator for its children.
        if (lv is IOrderableValue orderable) return orderable.Order(rv);

        // Residual scalars (number — which widens in its own tower — and coerced
        // raw strings) route through the one thin scalar leaf.
        return ScalarComparer.Order(lv, rv);
    }

    /// <summary>
    /// Structural equality on two element values — works for any type (used by
    /// <c>==</c>, group, unique). Collections own structural equality; scalars
    /// compare by value (with the if-path coercions).
    /// </summary>
    public static bool AreEqual(@this? left, @this? right) => AreEqualValues(left?.Materialize(), right?.Materialize());

    /// <summary>
    /// Structural equality on two raw values — the same path <see cref="AreEqual"/>
    /// uses, exposed for callers that hold unwrapped values (<c>contains</c>/<c>in</c>).
    /// A value owning its equality (<see cref="IEquatableValue"/>) decides; otherwise
    /// the coerced scalars route through <see cref="ScalarComparer"/>.
    /// </summary>
    public static bool AreEqualValues(object? lv, object? rv)
    {
        if (lv == null || rv == null) return lv == null && rv == null;

        // Coerce cross-type FIRST (text "5" vs number 5) so the self-dispatch below
        // runs on a reconciled pair — otherwise text.AreEqual(number) is trivially
        // false and "5" == 5 breaks.
        (lv, rv) = app.module.condition.Operator.NormalizeTypes(lv, rv);

        // The value owns its equality (collections: structural; each scalar wrapper).
        // It recurses back through this mediator for its children.
        if (lv is IEquatableValue equatable) return equatable.AreEqual(rv);

        return ScalarComparer.AreEqual(lv, rv);
    }
}
