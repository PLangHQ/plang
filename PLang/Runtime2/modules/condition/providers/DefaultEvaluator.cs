using System.Collections;
using System.Globalization;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.condition.providers;

public sealed class DefaultEvaluator : IEvaluator
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data Evaluate(If action)
    {
        var left = action.Left?.Value;
        var op = action.Operator;
        var right = action.Right?.Value;

        try
        {
            bool result = op == null ? IsTruthy(left) : EvaluateOp(left, op, right);
            return Data.Ok(result);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(left, op, right, ex);
        }
    }

    public Data Evaluate(Compare action)
    {
        var left = action.Left?.Value;
        var op = action.Operator;
        var right = action.Right?.Value;

        try
        {
            bool result = EvaluateOp(left, op, right);
            return Data.Ok(result);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(left, op, right, ex);
        }
    }

    // --- Core evaluation ---

    private bool EvaluateOp(object? left, string op, object? right)
    {
        (left, right) = NormalizeTypes(left, right);

        return op.ToLowerInvariant() switch
        {
            "==" => AreEqual(left, right),
            "!=" => !AreEqual(left, right),
            ">" => Compare(left, right) > 0,
            "<" => Compare(left, right) < 0,
            ">=" => Compare(left, right) >= 0,
            "<=" => Compare(left, right) <= 0,
            "contains" => Contains(left, right),
            "startswith" => StringOp(left, right, (s, r) => s.StartsWith(r, StringComparison.OrdinalIgnoreCase)),
            "endswith" => StringOp(left, right, (s, r) => s.EndsWith(r, StringComparison.OrdinalIgnoreCase)),
            "in" => In(left, right),
            "isempty" => IsEmpty(left),
            "not" => !IsTruthy(left),
            "and" => IsTruthy(left) && IsTruthy(right),
            "or" => IsTruthy(left) || IsTruthy(right),
            _ => throw new NotSupportedException($"Operator '{op}' is not supported")
        };
    }

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        int i => i != 0,
        long l => l != 0,
        double d => d != 0.0,
        decimal m => m != 0m,
        float f => f != 0f,
        string s => !string.IsNullOrWhiteSpace(s),
        ICollection c => c.Count > 0,
        _ => true
    };

    private static bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        if (left is string ls && right is string rs)
            return string.Equals(ls, rs, StringComparison.OrdinalIgnoreCase);

        return left.Equals(right);
    }

    private static int Compare(object? left, object? right)
    {
        if (left == null || right == null) return left == null && right == null ? 0 : (left == null ? -1 : 1);
        if (left is IComparable lc)
            return lc.CompareTo(right);
        throw new ArgumentException($"Type '{left.GetType().Name}' does not support comparison operators (>, <, >=, <=)");
    }

    private static bool Contains(object? left, object? right)
    {
        return left switch
        {
            string s when right is string sub => s.Contains(sub, StringComparison.OrdinalIgnoreCase),
            IEnumerable coll when left is not string => ContainsElement(coll, right),
            _ => false
        };
    }

    private static bool ContainsElement(IEnumerable coll, object? target)
    {
        foreach (var item in coll)
        {
            var (normalizedItem, normalizedTarget) = NormalizeTypes(item, target);
            if (AreEqual(normalizedItem, normalizedTarget))
                return true;
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

    private static bool In(object? left, object? right)
    {
        if (right is IEnumerable enumerable && right is not string)
            return ContainsElement(enumerable, left);
        return false;
    }

    private static bool IsEmpty(object? value)
    {
        if (value == null) return true;
        if (value is string s) return string.IsNullOrWhiteSpace(s);
        if (value is ICollection c) return c.Count == 0;
        return false;
    }

    private static (object? left, object? right) NormalizeTypes(object? left, object? right)
    {
        if (left == null || right == null) return (left, right);

        if (IsNumeric(left) && IsNumeric(right))
        {
            var targetType = WiderNumericType(left.GetType(), right.GetType());
            return (Convert.ChangeType(left, targetType, CultureInfo.InvariantCulture),
                    Convert.ChangeType(right, targetType, CultureInfo.InvariantCulture));
        }

        if (left is string ls && IsNumeric(right))
        {
            var converted = TryParseNumeric(ls);
            if (converted != null)
                return NormalizeTypes(converted, right);
        }
        if (right is string rs && IsNumeric(left))
        {
            var converted = TryParseNumeric(rs);
            if (converted != null)
                return NormalizeTypes(left, converted);
        }

        return (left, right);
    }

    private static bool IsNumeric(object? value) =>
        value is int or long or double or float or decimal or short or byte;

    private static readonly System.Type[] NumericOrder =
        { typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) };

    private static System.Type WiderNumericType(System.Type a, System.Type b)
    {
        var order = NumericOrder;
        var ai = Array.IndexOf(order, a);
        var bi = Array.IndexOf(order, b);
        if (ai < 0) ai = order.Length - 1;
        if (bi < 0) bi = order.Length - 1;
        return order[Math.Max(ai, bi)];
    }

    private static object? TryParseNumeric(string s)
    {
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    private static Data EvaluationError(object? left, string? op, object? right, Exception ex)
    {
        var leftType = left?.GetType().Name ?? "null";
        var rightType = right?.GetType().Name ?? "null";
        var message = op != null
            ? $"Condition evaluation failed: '{left}' ({leftType}) {op} '{right}' ({rightType}) — {ex.Message}"
            : $"Condition evaluation failed: IsTruthy('{left}' ({leftType})) — {ex.Message}";

        return Data.FromError(new ValidationError(message, "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = op != null
                ? $"Check that operator '{op}' is supported and that both operands are compatible types."
                : "Check that the left operand is a type that can be evaluated for truthiness."
        });
    }
}
