namespace app.data;

/// <summary>
/// Raised by an operator boundary when a <see cref="Comparison"/> result has no
/// honest answer for that operator — <see cref="Comparison.Incomparable"/> on any
/// comparison, <see cref="Comparison.NotEqual"/> on an ordering. Derives from
/// ArgumentException so the condition evaluator's catch surfaces it as a clean
/// EvaluationError. The VALUE never throws; the boundary decides error-or-result.
/// </summary>
public sealed class IncomparableException(string message) : System.ArgumentException(message);

/// <summary>
/// The single, sign-free result of every comparison. An enum by construction, not
/// an <c>int</c>: a magic <c>NotEqual = -2</c> would satisfy <c>&lt; 0</c> and a
/// sign-based sort would silently order "not equal" values. Nothing casts these to
/// numbers — the boundary maps each member to an operator value or a PLang error.
///
/// <para>The <see cref="NotEqual"/> vs <see cref="Incomparable"/> split is
/// load-bearing: it makes <c>dict == dict</c> work while <c>dict &lt; dict</c>
/// errors, and <c>dict == number</c> error while <c>%x% == null</c> does not.</para>
/// </summary>

public enum Comparison
{
    /// <summary>A real ordering: <c>this &lt; other</c>.</summary>
    Less,

    /// <summary>The two values are equal. <c>==</c> true; ordering ops resolve by operator.</summary>
    Equal,

    /// <summary>A real ordering: <c>this &gt; other</c>.</summary>
    Greater,

    /// <summary>
    /// Reconciled and <b>unequal, but no order</b> — equality-only types
    /// (<c>dict</c>/<c>bool</c>), or two differing values whose type has no order.
    /// Equality ops use it (<c>!=</c> true, <c>==</c> false); ordering ops error.
    /// Membership treats it as no-match, never an error.
    /// </summary>
    NotEqual,

    /// <summary>
    /// <b>Could not be reconciled at all</b> — a non-coercible cross-type pair
    /// (<c>dict</c> vs <c>number</c>). <em>Every</em> comparison/ordering operator
    /// errors. Membership still treats it as no-match and never errors.
    /// </summary>
    Incomparable,
}

/// <summary>
/// The two companions a <see cref="Comparison"/> needs but an enum can't carry
/// as instance methods. <see cref="Invert"/> preserves ordering when the RIGHT
/// operand drove the comparison; <see cref="AsSign"/> is the exit to .NET's
/// sort convention and throws where no total order exists.
/// </summary>
public static class ComparisonExtensions
{
    /// <summary>Flip an ordering result — used when the right operand drove
    /// (<c>b.Order(a)</c>) so the sign reads in the caller's <c>a,b</c> order.
    /// Equality and the no-order results are direction-free and pass through.</summary>
    public static Comparison Invert(this Comparison c) => c switch
    {
        Comparison.Less => Comparison.Greater,
        Comparison.Greater => Comparison.Less,
        _ => c,
    };

    /// <summary>The .NET sort-convention sign. A pair with no total order
    /// (<see cref="Comparison.NotEqual"/>/<see cref="Comparison.Incomparable"/>)
    /// has no honest sign — sort must surface that as an error, not park it.</summary>
    public static int AsSign(this Comparison c) => c switch
    {
        Comparison.Less => -1,
        Comparison.Equal => 0,
        Comparison.Greater => 1,
        _ => throw new System.InvalidOperationException("no ordering exists between these values"),
    };
}
