using System.Collections;
using Number = global::app.type.number.@this;

namespace app.data;

/// <summary>
/// The single typed-compare path. Both the condition operators (<c>&gt;</c>,
/// <c>&lt;</c>, <c>==</c> via <c>Operator</c>) and <c>list.sort</c> route through
/// here, so <c>if a.age &gt; b.age</c> and <c>sort by "age"</c> can never drift.
///
/// <para>Settled contract:
/// natural order within a type (number numeric incl. kind widening, datetime
/// chronological, duration by length, text lexical/invariant); nulls sort last;
/// ordering two genuinely different value types throws "cannot order X against Y";
/// orderable = number/datetime/duration/text; equality-only = dict/list/bool/table/null
/// (Compare throws, Equals works). The if-path coercions (numeric widening,
/// string↔number) are preserved via <see cref="app.module.condition.Operator.NormalizeTypes"/>.</para>
/// </summary>
public static class Compare
{
    /// <summary>
    /// Raised when two values cannot be ordered (different value types, or an
    /// equality-only type). Derives from ArgumentException so the condition
    /// evaluator's catch filter surfaces it as a clean EvaluationError.
    /// </summary>
    public sealed class NotOrderableException(string message) : System.ArgumentException(message);

    private static readonly System.Collections.Generic.HashSet<string> Orderable =
        new(System.StringComparer.Ordinal) { "number", "datetime", "duration", "text" };

    /// <summary>
    /// Total order on two element values. Nulls sort last; same orderable type
    /// compares naturally; different value types — or an equality-only type —
    /// throw <see cref="NotOrderableException"/>.
    /// </summary>
    public static int Order(@this? left, @this? right)
    {
        object? lv = left?.Value;
        object? rv = right?.Value;

        // Nulls sort last: null is the greatest element.
        if (lv == null && rv == null) return 0;
        if (lv == null) return 1;
        if (rv == null) return -1;

        // Preserve the if-path coercions (numeric widening, string↔number).
        (lv, rv) = app.module.condition.Operator.NormalizeTypes(lv, rv);

        var lf = Family(lv);
        var rf = Family(rv);
        if (!Orderable.Contains(lf))
            throw new NotOrderableException($"cannot order {lf} — it is an equality-only type (no natural ordering)");
        if (lf != rf)
            throw new NotOrderableException($"cannot order {lf} against {rf}");

        return lf switch
        {
            "number"   => Number.FromObject(lv)!.CompareTo(Number.FromObject(rv)),
            "text"     => string.CompareOrdinal((string)lv, (string)rv),
            "duration" => ((System.TimeSpan)lv).CompareTo((System.TimeSpan)rv),
            "datetime" => ToOffset(lv).CompareTo(ToOffset(rv)),
            _          => throw new NotOrderableException($"cannot order {lf}"),
        };
    }

    /// <summary>
    /// Structural equality on two element values — works for any type (used by
    /// <c>==</c>, group, unique). Scalars compare by value (with the if-path
    /// coercions); dict/list compare structurally so equivalent collections collapse.
    /// </summary>
    public static bool AreEqual(@this? left, @this? right) => AreEqualValues(left?.Value, right?.Value);

    private static bool AreEqualValues(object? lv, object? rv)
    {
        if (lv == null || rv == null) return lv == null && rv == null;

        if (lv is dict ld && rv is dict rd)
        {
            if (ld.Count != rd.Count) return false;
            foreach (var entry in ld.Entries)
            {
                var other = rd.Get(entry.Name);
                if (other == null || !AreEqualValues(entry.Value, other.Value)) return false;
            }
            return true;
        }

        if (lv is app.type.list.@this ll && rv is app.type.list.@this rl)
        {
            if (ll.Count != rl.Count) return false;
            for (int i = 0; i < ll.Count; i++)
                if (!AreEqualValues(ll.At(i)!.Value, rl.At(i)!.Value)) return false;
            return true;
        }

        (lv, rv) = app.module.condition.Operator.NormalizeTypes(lv, rv);
        if (lv is string ls && rv is string rs)
            return string.Equals(ls, rs, System.StringComparison.OrdinalIgnoreCase);
        return lv!.Equals(rv);
    }

    /// <summary>The PLang value family of a raw value — the axis ordering and equality-only-ness key off.</summary>
    public static string Family(object? value) => value switch
    {
        null => "null",
        bool => "bool",
        string => "text",
        dict => "dict",
        app.type.list.@this => "list",
        System.TimeSpan => "duration",
        System.DateTime or System.DateTimeOffset or System.DateOnly => "datetime",
        Number => "number",
        _ when value is int or long or short or byte or float or double or decimal => "number",
        _ => value.GetType().Name.ToLowerInvariant(),
    };

    private static System.DateTimeOffset ToOffset(object value) => value switch
    {
        System.DateTimeOffset dto => dto,
        System.DateTime dt => new System.DateTimeOffset(dt),
        System.DateOnly d => new System.DateTimeOffset(d.ToDateTime(System.TimeOnly.MinValue)),
        _ => throw new NotOrderableException($"cannot order datetime value of CLR type {value.GetType().Name}"),
    };
}
