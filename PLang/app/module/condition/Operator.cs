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
            // Equality + ordering route through THE comparison entry (data.Compare —
            // rank picks the driver, the driver's typed hook compares) and this boundary
            // maps the sign-free Comparison per operator. The value never throws; the
            // boundary turns NotEqual-on-ordering and Incomparable into EvaluationError.
            ["=="] = Equal,
            ["!="] = async (l, r) => !await Equal(l, r),
            [">"] = (l, r) => Ordered(l, r, ">", c => c == global::app.data.Comparison.Greater),
            ["<"] = (l, r) => Ordered(l, r, "<", c => c == global::app.data.Comparison.Less),
            [">="] = (l, r) => Ordered(l, r, ">=", c => c is global::app.data.Comparison.Greater or global::app.data.Comparison.Equal),
            ["<="] = (l, r) => Ordered(l, r, "<=", c => c is global::app.data.Comparison.Less or global::app.data.Comparison.Equal),
            ["contains"] = Contains,
            ["startswith"] = (l, r) => Task.FromResult(StringOp(Val(l), Val(r), (s, v) => s.StartsWith(v, StringComparison.OrdinalIgnoreCase))),
            ["endswith"] = (l, r) => Task.FromResult(StringOp(Val(l), Val(r), (s, v) => s.EndsWith(v, StringComparison.OrdinalIgnoreCase))),
            ["in"] = (l, r) => Contains(r, l),
            ["isempty"] = (l, _) => Task.FromResult(IsEmpty(Val(l))),
            // `%x% is dict` / `is number` / `is item` — IS-A query against the
            // value-type lattice. The right operand is the PLang type name. `item`
            // is the apex (true for any value).
            ["is"] = (l, r) => Task.FromResult(IsType(l, r)),
            ["and"] = async (l, r) => await IsTruthy(l) && await IsTruthy(r),
            ["or"] = async (l, r) => await IsTruthy(l) || await IsTruthy(r),
        };

    // Ordering boundary: Less/Equal/Greater answer by operator; NotEqual and
    // Incomparable have no honest order — error, never a silent false.
    private static async Task<bool> Ordered(data.@this? l, data.@this? r, string op,
        Func<global::app.data.Comparison, bool> map)
    {
        if (l == null || r == null)
            throw new global::app.data.IncomparableException($"cannot order a missing operand with '{op}'");
        var c = await l.Compare(r);
        if (c is global::app.data.Comparison.NotEqual or global::app.data.Comparison.Incomparable)
            throw new global::app.data.IncomparableException(
                $"cannot order '{l.Type.Name}' and '{r.Type.Name}' values with '{op}'");
        return map(c);
    }

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
        if ((right == null ? null : await right.Value()) is bool rb && (left == null ? null : await left.Value()) is not bool)
        {
            bool leftTruthy = left != null && await left.ToBooleanAsync();
            return rb ? leftTruthy : !leftTruthy;
        }

        if (left == null || right == null) return left == null && right == null;

        // THE comparison entry; the equality boundary: Equal → true, Less/Greater/
        // NotEqual → false, Incomparable → error (dict == number has no honest answer).
        var c = await left.Compare(right);
        if (c == global::app.data.Comparison.Incomparable)
            throw new global::app.data.IncomparableException(
                $"'{left.Type.Name}' and '{right.Type.Name}' values cannot be compared with '=='");
        return c == global::app.data.Comparison.Equal;
    }

    // --- Collection/String operators ---

    // Membership: a substring test for text-in-text, element membership otherwise.
    // Matches ONLY on Equal and never errors — NotEqual/Incomparable mean "not this
    // one" (the table's membership column), so a mixed list never blows a `contains`.
    private static async Task<bool> Contains(data.@this? left, data.@this? right)
    {
        var lv = left?.Materialize();
        var rv = right?.Materialize();
        if (lv is global::app.type.text.@this lt && rv is global::app.type.text.@this rt)
            return lt.Value.Contains(rt.Value, StringComparison.OrdinalIgnoreCase);
        if (lv is string ls && rv is string rs)
            return ls.Contains(rs, StringComparison.OrdinalIgnoreCase);
        if (left == null || right == null) return false;

        if (lv is app.type.list.@this list)
        {
            foreach (var item in list.Items)
                if (await item.Compare(right) == global::app.data.Comparison.Equal) return true;
            return false;
        }
        if (lv is IEnumerable coll and not string)
        {
            foreach (var item in coll)
            {
                var element = item as data.@this ?? new data.@this("", item);
                if (await element.Compare(right) == global::app.data.Comparison.Equal) return true;
            }
            return false;
        }
        return false;
    }

    private static bool StringOp(object? left, object? right, Func<string, string, bool> op)
    {
        var ls = left?.ToString();
        var rs = right?.ToString();
        if (ls == null || rs == null) return false;
        return op(ls, rs);
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

}
