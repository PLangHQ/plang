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
        object? lv = left?.Value;
        object? rv = right?.Value;

        // Nulls sort last: null is the greatest element.
        if (lv == null && rv == null) return 0;
        if (lv == null) return 1;
        if (rv == null) return -1;

        // The value owns its order (lists: lexicographic). It recurses back through
        // this mediator for its children.
        if (lv is IOrderableValue orderable) return orderable.Order(rv);

        // Scalars: coerce (numeric widening, string↔number), then the one scalar leaf.
        (lv, rv) = app.module.condition.Operator.NormalizeTypes(lv, rv);
        return ScalarComparer.Order(lv, rv);
    }

    /// <summary>
    /// Structural equality on two element values — works for any type (used by
    /// <c>==</c>, group, unique). Collections own structural equality; scalars
    /// compare by value (with the if-path coercions).
    /// </summary>
    public static bool AreEqual(@this? left, @this? right) => AreEqualValues(left?.Value, right?.Value);

    /// <summary>
    /// Structural equality on two raw values — the same path <see cref="AreEqual"/>
    /// uses, exposed for callers that hold unwrapped values (<c>contains</c>/<c>in</c>).
    /// A value owning its equality (<see cref="IEquatableValue"/>) decides; otherwise
    /// the coerced scalars route through <see cref="ScalarComparer"/>.
    /// </summary>
    public static bool AreEqualValues(object? lv, object? rv)
    {
        if (lv == null || rv == null) return lv == null && rv == null;

        // The value owns its equality (collections: structural). It recurses back
        // through this mediator for its children.
        if (lv is IEquatableValue equatable) return equatable.AreEqual(rv);

        (lv, rv) = app.module.condition.Operator.NormalizeTypes(lv, rv);
        return ScalarComparer.AreEqual(lv, rv);
    }
}
