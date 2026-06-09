using System.Collections;
using System.Globalization;
using System.Threading.Tasks;

namespace app.module.condition;

/// <summary>
/// Represents a condition operator in PLang. Owns both the identity (which operator)
/// and the behavior (how to evaluate). Single source of truth — adding an operator
/// means adding one entry to the Registry.
/// Receives Data objects — unwraps to raw values only at the point of comparison.
/// </summary>
public sealed class Operator
{
    // Evaluators are async: a Data value may be IBooleanResolvable (a path,
    // whose truthiness is "does it exist" — I/O for the http scheme). Pure-sync
    // comparisons wrap their result in Task.FromResult.
    private static readonly Dictionary<string, Func<data.@this?, data.@this?, Task<bool>>> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["=="] = Equal,
            ["!="] = async (l, r) => !await Equal(l, r),
            // Ordering routes through the one typed-compare path (app.data.Compare) so
            // `if a > b` and `sort by …` can never drift. It throws for equality-only
            // types and genuinely-different value types — surfaced as the step error.
            // A null operand is incomparable on the if-path (returns false), distinct
            // from sort's nulls-last — sort calls Order directly.
            [">"] = (l, r) => Task.FromResult(BothPresent(l, r) && global::app.data.Compare.Order(l, r) > 0),
            ["<"] = (l, r) => Task.FromResult(BothPresent(l, r) && global::app.data.Compare.Order(l, r) < 0),
            [">="] = (l, r) => Task.FromResult(BothPresent(l, r) && global::app.data.Compare.Order(l, r) >= 0),
            ["<="] = (l, r) => Task.FromResult(BothPresent(l, r) && global::app.data.Compare.Order(l, r) <= 0),
            ["contains"] = (l, r) => Task.FromResult(Contains(Val(l), Val(r))),
            ["startswith"] = (l, r) => Task.FromResult(StringOp(Val(l), Val(r), (s, v) => s.StartsWith(v, StringComparison.OrdinalIgnoreCase))),
            ["endswith"] = (l, r) => Task.FromResult(StringOp(Val(l), Val(r), (s, v) => s.EndsWith(v, StringComparison.OrdinalIgnoreCase))),
            ["in"] = (l, r) => Task.FromResult(In(Val(l), Val(r))),
            ["isempty"] = (l, _) => Task.FromResult(IsEmpty(Val(l))),
            // `%x% is dict` / `is number` / `is item` — IS-A query against the
            // value-type lattice. The right operand is the PLang type name. `item`
            // is the apex (true for any value).
            ["is"] = (l, r) => Task.FromResult(IsType(l, r)),
            ["and"] = async (l, r) => await IsTruthy(l) && await IsTruthy(r),
            ["or"] = async (l, r) => await IsTruthy(l) || await IsTruthy(r),
        };

    [app.Attributes.Choices]
    public static string[] Choices(actor.context.@this? context) => [.. Registry.Keys];

    [Out] public string Value { get; }
    public Func<data.@this?, data.@this?, Task<bool>> Evaluate { get; }

    public Operator(string value)
    {
        if (!Registry.TryGetValue(value, out var eval))
            throw new ArgumentException(
                $"Unsupported operator: '{value}'. Valid: {string.Join(", ", Registry.Keys)}");
        Value = value.ToLowerInvariant();
        Evaluate = eval;
    }

    public static implicit operator string(Operator op) => op.Value;
    public static implicit operator Operator(string s) => new(s);
    public override string ToString() => Value;

    // --- Helpers ---

    /// <summary>Unwrap Data to raw value.</summary>
    private static object? Val(data.@this? data) => data?.Materialize();

    /// <summary>Both operands have a non-null value — the ordering operators are false otherwise.</summary>
    private static bool BothPresent(data.@this? left, data.@this? right) => left?.Materialize() != null && right?.Materialize() != null;

    /// <summary>IS-A: does the left value's type satisfy the named type (right operand)?</summary>
    private static bool IsType(data.@this? left, data.@this? right)
    {
        var typeName = right?.Materialize()?.ToString();
        if (left == null || string.IsNullOrWhiteSpace(typeName)) return false;
        return left.Type.Is(typeName);
    }

    /// <summary>
    /// Truthy check on Data. Routes through <c>Data.ToBooleanAsync()</c> so an
    /// <see cref="app.data.IBooleanResolvable"/> value (a path) answers for
    /// itself; otherwise the usual null/false/0/"" rules apply.
    /// </summary>
    public static async Task<bool> IsTruthy(data.@this? data)
    {
        if (data == null) return false;
        return await data.ToBooleanAsync();
    }

    // --- Equality ---

    private static async Task<bool> Equal(data.@this? left, data.@this? right)
    {
        // == true with non-bool left: delegates to Data.ToBooleanAsync(), so an
        // IBooleanResolvable left (a path) answers `if %path% exists` itself.
        if (right?.Materialize() is bool rb && left?.Materialize() is not bool)
        {
            bool leftTruthy = left != null && await left.ToBooleanAsync();
            return rb ? leftTruthy : !leftTruthy;
        }

        // == true/false with bool left: structural equality via the one compare path
        // (so equivalent dicts/lists compare equal, matching group/unique).
        return global::app.data.Compare.AreEqual(left, right);
    }

    // --- Collection/String operators ---

    private static bool Contains(object? left, object? right)
    {
        // Born-native: a text value rides as text.@this — unwrap to string so the
        // substring arm fires (the wrapper isn't a CLR string).
        if (left is global::app.type.text.@this lt) left = lt.Value;
        if (right is global::app.type.text.@this rt) right = rt.Value;
        return left switch
        {
            string s when right is string sub => s.Contains(sub, StringComparison.OrdinalIgnoreCase),
            app.type.list.@this list => ContainsValue(list, right),
            IEnumerable coll when left is not string => ContainsElement(coll, right),
            _ => false
        };
    }

    // Membership routes through the one compare path (Compare.AreEqualValues) so
    // `%list% contains %x%` agrees with `%elem% == %x%` — structural for dict/list,
    // case-insensitive for text. A native list holds Data; raw IEnumerable holds values.
    private static bool ContainsValue(app.type.list.@this list, object? target)
    {
        foreach (var item in list.Items)
            if (global::app.data.Compare.AreEqualValues(item.Value, target)) return true;
        return false;
    }

    private static bool ContainsElement(IEnumerable coll, object? target)
    {
        foreach (var item in coll)
            if (global::app.data.Compare.AreEqualValues(item, target)) return true;
        return false;
    }

    private static bool StringOp(object? left, object? right, Func<string, string, bool> op)
    {
        var ls = left?.ToString();
        var rs = right?.ToString();
        if (ls == null || rs == null) return false;
        return op(ls, rs);
    }

    private static bool In(object? left, object? right)
    {
        if (right is app.type.list.@this list)
            return ContainsValue(list, left);
        if (right is IEnumerable enumerable and not string)
            return ContainsElement(enumerable, left);
        return false;
    }

    private static bool IsEmpty(object? value)
    {
        if (value == null) return true;
        if (value is global::app.type.text.@this t) value = t.Value;
        if (value is global::app.type.@null.@this) return true;
        if (value is string s) return string.IsNullOrWhiteSpace(s);
        if (value is app.type.dict.@this d) return d.Entries.Count == 0;
        if (value is app.type.list.@this l) return l.Count == 0;
        if (value is ICollection c) return c.Count == 0;
        return false;
    }

    // --- Type normalization ---

    // The one binary-coercion mediator. Post-born-native it inspects WRAPPER types
    // (text/number, and raw Enum which is not a value wrapper) — the one blessed
    // cross-type reconciliation site. Numeric widening (5L == 5.0 == 5m) is NOT here:
    // both sides are number.@this and number's own tower (CompareTo) widens. This only
    // bridges the genuinely-different types: text↔number ("5" == 5) and enum↔text.
    public static (object? left, object? right) NormalizeTypes(object? left, object? right)
    {
        if (left == null || right == null) return (left, right);

        // text <-> number: parse the text so "5" == 5 and "5" < 6 coerce through the
        // number tower. Inspects the value's PLang shape (text/number) but tolerates a
        // raw string/CLR-numeric that slips through a perimeter.
        if (IsTextLike(left, out var lts) && IsNumberLike(right))
        {
            var n = TryNumber(lts);
            if (n != null) return (n, right);
        }
        if (IsTextLike(right, out var rts) && IsNumberLike(left))
        {
            var n = TryNumber(rts);
            if (n != null) return (left, n);
        }

        // enum <-> text: an enum field compares by its name against a text literal
        // (`where Status equals 'Timeout'`). Enums aren't value wrappers (they arrive
        // raw); coerce both sides to their string form.
        if (left is Enum le && IsTextLike(right, out var res)) return (le.ToString(), res);
        if (right is Enum re && IsTextLike(left, out var les)) return (les, re.ToString());

        // typed-scalar <-> text: a value that can be born from text (date/time/datetime/
        // duration) parses the text operand into its own type, so `%date% == "2026-01-01"`
        // coerces its ISO form the way "5" == 5 does. The type owns the parse (ITextCoercible);
        // the mediator only delegates. A null parse leaves the pair unreconciled (compare false).
        if (left is global::app.data.ITextCoercible lc && IsTextLike(right, out var rcs)
            && lc.CoerceText(rcs) is { } lcoerced) return (left, lcoerced);
        if (right is global::app.data.ITextCoercible rc && IsTextLike(left, out var lcs)
            && rc.CoerceText(lcs) is { } rcoerced) return (rcoerced, right);

        return (left, right);
    }

    // A value that carries text content — the text wrapper or (perimeter) a raw string.
    private static bool IsTextLike(object? v, out string s)
    {
        switch (v)
        {
            case global::app.type.text.@this t: s = t.Value; return true;
            case string str: s = str; return true;
            default: s = ""; return false;
        }
    }

    // A value that carries a number — the number wrapper or (perimeter) a raw CLR numeric.
    private static bool IsNumberLike(object? v) =>
        v is global::app.type.number.@this
        || v is int or long or short or byte or float or double or decimal;

    private static global::app.type.number.@this? TryNumber(string s)
    {
        if (long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var l))
            return global::app.type.number.@this.From(l);
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return global::app.type.number.@this.From(d);
        return null;
    }
}
