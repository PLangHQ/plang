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
            ["startswith"] = async (l, r) => StringOp(await Val(l), await Val(r), (s, v) => s.StartsWith(v, StringComparison.OrdinalIgnoreCase)),
            ["endswith"] = async (l, r) => StringOp(await Val(l), await Val(r), (s, v) => s.EndsWith(v, StringComparison.OrdinalIgnoreCase)),
            ["in"] = (l, r) => Contains(r, l),
            // The ITEM owns emptiness (text → whitespace-only, containers →
            // zero entries, null/absent → empty); the binding answers absence.
            ["isempty"] = async (l, _) => l == null || await l.IsEmpty(),
            // `%x% is dict` / `is number` / `is item` — IS-A query against the
            // value-type lattice. The right operand is the PLang type name. `item`
            // is the apex (true for any value).
            ["is"] = IsType,
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

    /// <summary>The value through the door — the type makes itself ready
    /// (load/parse/render); the answer is the typed instance.</summary>
    private static async ValueTask<global::app.type.item.@this?> Val(data.@this? data)
        => data == null ? null : await data.Value();

    /// <summary>Both operands have a present value — the ordering operators are false otherwise.</summary>
    private static bool BothPresent(data.@this? left, data.@this? right)
        => left?.HasValue == true && right?.HasValue == true;

    /// <summary>
    /// IS-A: does the left value's type satisfy the named type (right operand)?
    /// On an un-narrowed reference (`file`/`url`) a miss forces the narrow —
    /// `is dict` IS an examination of the content, so the answer is
    /// deterministic on both branches. `is file` answers from the chain with
    /// no read.
    /// </summary>
    private static async Task<bool> IsType(data.@this? left, data.@this? right)
    {
        var typeName = right?.Peek()?.ToString();
        if (left == null || string.IsNullOrWhiteSpace(typeName)) return false;
        if (left.Type.Is(typeName)) return true;
        if (left.Peek() is global::app.type.file.@this or global::app.type.url.@this
            || left.RawUntouched)
        {
            // `is <type>` IS an examination — the door parses + narrows, then
            // the chain answers deterministically on both branches.
            _ = await left.Value();
            return left.Type.Is(typeName);
        }
        return false;
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
        // A bool rides born-native as bool.@this — unwrap both shapes.
        var rv = right == null ? null : await right.Value();
        bool? rb = (rv as global::app.type.@bool.@this)?.Value;
        var lv = left == null ? null : await left.Value();
        bool leftIsBool = lv is global::app.type.@bool.@this;
        if (rb != null && !leftIsBool)
        {
            bool leftTruthy = left != null && await left.ToBooleanAsync();
            return rb.Value ? leftTruthy : !leftTruthy;
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

    // Membership — the ITEM owns the answer (text substring, list element
    // equality through THE comparison entry, dict key, directory listing).
    private static async Task<bool> Contains(data.@this? left, data.@this? right)
    {
        if (left == null || right == null) return false;
        var lv = await Val(left);
        if (lv == null) return false;
        return await lv.Contains(right);
    }

    private static bool StringOp(object? left, object? right, Func<string, string, bool> op)
    {
        // The text face of each operand — startswith/endswith are text
        // questions; a non-text answers through its canonical text form.
        var ls = left?.ToString();
        var rs = right?.ToString();
        if (ls == null || rs == null) return false;
        return op(ls, rs);
    }
}
