using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using Answer = global::app.data.@this<global::app.type.item.@bool.@this>;

namespace app.module.action.condition;

/// <summary>
/// Represents a condition operator in PLang. Owns both the identity (which operator)
/// and the behavior (how to evaluate). Single source of truth — adding an operator
/// means adding one entry to the Registry.
/// Receives Data objects — unwraps to raw values only at the point of comparison.
/// Every operator answers a plang bool — true, false, or the developer's error
/// (<c>is foo</c>, ordering values that have no order) — never a thrown exception.
/// </summary>
[global::app.Attributes.PlangType("operator")]
public sealed class Operator
{
    // Evaluators are async: a Data value may be IBooleanResolvable (a path,
    // whose truthiness is "does it exist" — I/O for the http scheme). The answer is
    // born with the evaluating context — an operand may be absent.
    private static readonly Dictionary<string, Func<data.@this?, data.@this?, actor.context.@this, Task<Answer>>> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Equality + ordering route through THE comparison entry (data.Compare —
            // rank picks the driver, the driver's typed hook compares) and this boundary
            // maps the sign-free Comparison per operator. The value never throws; the
            // boundary answers NotEqual-on-ordering and Incomparable with an error.
            // Every question has its negative beside it: the positive's answer inverted (an error
            // stays the error). The negation is the operator's, so a step never carries a flag
            // that flips its logic.
            ["=="] = Equal,
            ["!="] = Negated(Equal),
            [">"] = (l, r, c) => Ordered(l, r, c, ">", x => x == global::app.data.Comparison.Greater),
            ["<"] = (l, r, c) => Ordered(l, r, c, "<", x => x == global::app.data.Comparison.Less),
            [">="] = (l, r, c) => Ordered(l, r, c, ">=", x => x is global::app.data.Comparison.Greater or global::app.data.Comparison.Equal),
            ["<="] = (l, r, c) => Ordered(l, r, c, "<=", x => x is global::app.data.Comparison.Less or global::app.data.Comparison.Equal),
            ["contains"] = ContainsOp,
            ["notcontains"] = Negated(ContainsOp),
            ["startswith"] = StartsWith,
            ["notstartswith"] = Negated(StartsWith),
            ["endswith"] = EndsWith,
            ["notendswith"] = Negated(EndsWith),
            ["in"] = In,
            ["notin"] = Negated(In),
            // The ITEM owns emptiness (text → whitespace-only, containers →
            // zero entries, null/absent → empty); the binding answers absence.
            ["isempty"] = IsEmpty,
            ["isnotempty"] = Negated(IsEmpty),
            // `%x% is dict` / `is number` / `is item` — IS-A query against the
            // value-type lattice. The right operand is the PLang type name. `item`
            // is the apex (true for any value).
            ["is"] = IsType,
            ["isnot"] = Negated(IsType),
            // A failed operand is an error that passes through.
            ["and"] = (l, r, c) => Logical(l, r, c, and: true),
            ["or"] = (l, r, c) => Logical(l, r, c, and: false),
        };

    // Ordering boundary: Less/Equal/Greater answer by operator; NotEqual and
    // Incomparable have no honest order — an error, never a silent false.
    private static async Task<Answer> Ordered(data.@this? l, data.@this? r, actor.context.@this context, string op,
        Func<global::app.data.Comparison, bool> map)
    {
        if (l == null || r == null)
            return Refused(context, $"cannot order a missing operand with '{op}'", "EvaluationError");
        var c = await l.Compare(r);
        if (c is global::app.data.Comparison.NotEqual or global::app.data.Comparison.Incomparable)
            return Refused(context, $"cannot order '{l.Type.Name}' and '{r.Type.Name}' values with '{op}'", "EvaluationError");
        return Answer(context, map(c));
    }

    [app.Attributes.Choices]
    public static string[] Choices(actor.context.@this? context) => [.. Registry.Keys];

    [Out] public string Value { get; }
    public Func<data.@this?, data.@this?, actor.context.@this, Task<Answer>> Evaluate { get; }

    public Operator(string value)
    {
        if (!Registry.TryGetValue(value, out var eval))
            throw new ArgumentException(
                $"Unsupported operator: '{value}'. Valid: {string.Join(", ", Registry.Keys)}");
        Value = value.ToLowerInvariant();
        Evaluate = eval;
    }

    /// <summary>The operator asks of Left alone: emptiness takes no Right.</summary>
    public bool IsUnary => Value is "isempty" or "isnotempty";

    /// <summary>What's wrong with this operator's operands as written, or null: emptiness asks of Left
    /// alone; every other operator compares Left with a Right (<c>Right=null</c> written is a Right).</summary>
    public global::app.error.Error? Operands(bool right) =>
        IsUnary && right ? new global::app.error.Error($"'{Value}' asks of Left alone — leave Right out", "OperandExtra", 400)
        : !IsUnary && !right ? new global::app.error.Error(
            $"'{Value}' compares Left with Right, and Right is missing — for Left's own truth (is it true, does it exist, is it set) leave Operator out", "OperandMissing", 400)
        : null;

    public static implicit operator string(Operator op) => op.Value;
    public static implicit operator Operator(string s) => new(s);
    public override string ToString() => Value;

    // --- Helpers ---

    private static Answer Answer(actor.context.@this context, bool value)
        => context.Ok<global::app.type.item.@bool.@this>(value);

    private static Answer Refused(actor.context.@this context, string message, string key)
        => context.Error<global::app.type.item.@bool.@this>(new global::app.error.ServiceError(message, key, 400));

    // The negation of an answer; an error stays the error.
    private static Answer Not(Answer answer, actor.context.@this context)
        => answer.Success ? Answer(context, !answer.ToBoolean()) : answer;

    // A negative operator: its positive, the answer inverted.
    private static Func<data.@this?, data.@this?, actor.context.@this, Task<Answer>> Negated(
        Func<data.@this?, data.@this?, actor.context.@this, Task<Answer>> positive)
        => async (l, r, c) => Not(await positive(l, r, c), c);

    private static async Task<Answer> ContainsOp(data.@this? l, data.@this? r, actor.context.@this c)
        => Answer(c, await Contains(l, r));

    private static async Task<Answer> StartsWith(data.@this? l, data.@this? r, actor.context.@this c)
        => Answer(c, StringOp(await Val(l), await Val(r), (s, v) => s.StartsWith(v, StringComparison.OrdinalIgnoreCase)));

    private static async Task<Answer> EndsWith(data.@this? l, data.@this? r, actor.context.@this c)
        => Answer(c, StringOp(await Val(l), await Val(r), (s, v) => s.EndsWith(v, StringComparison.OrdinalIgnoreCase)));

    private static async Task<Answer> In(data.@this? l, data.@this? r, actor.context.@this c)
        => Answer(c, await Contains(r, l));

    private static async Task<Answer> IsEmpty(data.@this? l, data.@this? _, actor.context.@this c)
        => Answer(c, l == null || await l.IsEmpty());

    private static async Task<Answer> Logical(data.@this? l, data.@this? r, actor.context.@this context, bool and)
    {
        if (l is { Success: false }) return context.Error<global::app.type.item.@bool.@this>(l.Error!);
        var left = await IsTruthy(l);
        if (left != and) return Answer(context, left);
        if (r is { Success: false }) return context.Error<global::app.type.item.@bool.@this>(r.Error!);
        return Answer(context, await IsTruthy(r));
    }

    /// <summary>The value through the door — the type makes itself ready
    /// (load/parse/render); the answer is the typed instance.</summary>
    private static async ValueTask<global::app.type.item.@this?> Val(data.@this? data)
        => data == null ? null : await data.Value();

    /// <summary>
    /// IS-A: does the left value's type satisfy the named type (right operand)?
    /// On an un-narrowed reference (`file`/`url`) a miss forces the narrow —
    /// `is dict` IS an examination of the content, so the answer is
    /// deterministic on both branches. `is file` answers from the chain with
    /// no read. A type name the developer wrote that does not exist is their error.
    /// </summary>
    private static async Task<Answer> IsType(data.@this? left, data.@this? right, actor.context.@this context)
    {
        var typeName = right?.Peek()?.ToString();
        if (left == null || string.IsNullOrWhiteSpace(typeName)) return Answer(context, false);
        if (!context.App.Type.Contains(typeName.Split('/')[0]))
            return Refused(context, $"Unknown type '{typeName}'", "UnknownType");
        // Ask the VALUE — it walks its own provenance chain (a narrowed dict still answers `is file`).
        if (left.Is(typeName)) return Answer(context, true);
        if (left.Peek() is global::app.type.item.file.@this or global::app.type.item.url.@this
            || left.RawUntouched)
        {
            // `is <type>` IS an examination — the door parses + narrows, then
            // the value answers deterministically from its retained provenance.
            _ = await left.Value();
            return Answer(context, left.Is(typeName));
        }
        return Answer(context, false);
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

    private static async Task<Answer> Equal(data.@this? left, data.@this? right, actor.context.@this context)
    {
        // == true with non-bool left: delegates to Data.ToBooleanAsync(), so an
        // IBooleanResolvable left (a path) answers `if %path% exists` itself.
        // A bool rides born-native as bool.@this — unwrap both shapes.
        var rv = right == null ? null : await right.Value();
        bool? rb = (rv as global::app.type.item.@bool.@this)?.Value;
        var lv = left == null ? null : await left.Value();
        bool leftIsBool = lv is global::app.type.item.@bool.@this;
        if (rb != null && !leftIsBool)
        {
            bool leftTruthy = left != null && await left.ToBooleanAsync();
            return Answer(context, rb.Value ? leftTruthy : !leftTruthy);
        }

        if (left == null || right == null) return Answer(context, left == null && right == null);

        // THE comparison entry; the equality boundary: Equal → true, Less/Greater/
        // NotEqual → false, Incomparable → error (dict == number has no honest answer).
        var c = await left.Compare(right);
        if (c == global::app.data.Comparison.Incomparable)
            return Refused(context, $"'{left.Type.Name}' and '{right.Type.Name}' values cannot be compared with '=='", "EvaluationError");
        return Answer(context, c == global::app.data.Comparison.Equal);
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
