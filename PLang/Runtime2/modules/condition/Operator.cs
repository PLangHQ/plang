using System.Collections;
using System.Globalization;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.condition;

/// <summary>
/// Represents a condition operator in PLang. Owns both the identity (which operator)
/// and the behavior (how to evaluate). Single source of truth — adding an operator
/// means adding one entry to the Registry.
/// Receives Data objects — unwraps to raw values only at the point of comparison.
/// </summary>
public sealed class Operator : IObject
{
    private static readonly Dictionary<string, Func<Data?, Data?, bool>> Registry =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["=="] = Equal,
            ["!="] = (l, r) => !Equal(l, r),
            [">"] = (l, r) => Compare(Val(l), Val(r)) > 0,
            ["<"] = (l, r) => Compare(Val(l), Val(r)) < 0,
            [">="] = (l, r) => Compare(Val(l), Val(r)) >= 0,
            ["<="] = (l, r) => Compare(Val(l), Val(r)) <= 0,
            ["contains"] = (l, r) => Contains(Val(l), Val(r)),
            ["startswith"] = (l, r) => StringOp(Val(l), Val(r), (s, v) => s.StartsWith(v, StringComparison.OrdinalIgnoreCase)),
            ["endswith"] = (l, r) => StringOp(Val(l), Val(r), (s, v) => s.EndsWith(v, StringComparison.OrdinalIgnoreCase)),
            ["in"] = (l, r) => In(Val(l), Val(r)),
            ["isempty"] = (l, _) => IsEmpty(Val(l)),
            ["and"] = (l, r) => IsTruthy(l) && IsTruthy(r),
            ["or"] = (l, r) => IsTruthy(l) || IsTruthy(r),
        };

    public static string[] ValidValues => [.. Registry.Keys];

    public string Value { get; }
    public Func<Data?, Data?, bool> Evaluate { get; }

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
    private static object? Val(Data? data) => data?.Value;

    /// <summary>
    /// Truthy check on Data. If the value is bool, check the bool.
    /// If the value is not bool, check IsInitialized — "does this variable exist?"
    /// </summary>
    public static bool IsTruthy(Data? data)
    {
        if (data == null) return false;
        if (data.Value is bool b) return b;
        return data.ToBoolean();
    }

    // --- Equality ---

    private static bool Equal(Data? left, Data? right)
    {
        // == true with non-bool left: delegates to Data.ToBoolean()
        if (right?.Value is bool rb && left?.Value is not bool)
            return rb ? left?.ToBoolean() == true : left?.ToBoolean() != true;

        // == true/false with bool left: normal equality
        return AreEqual(Val(left), Val(right));
    }

    private static bool AreEqual(object? left, object? right)
    {
        (left, right) = NormalizeTypes(left, right);
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        if (left is string ls && right is string rs)
            return string.Equals(ls, rs, StringComparison.OrdinalIgnoreCase);

        return left.Equals(right);
    }

    // --- Comparison ---

    private static int Compare(object? left, object? right)
    {
        (left, right) = NormalizeTypes(left, right);
        if (left == null || right == null)
            return left == null && right == null ? 0 : (left == null ? -1 : 1);
        if (left is IComparable lc)
            return lc.CompareTo(right);
        throw new ArgumentException(
            $"Type '{left.GetType().Name}' does not support comparison operators (>, <, >=, <=)");
    }

    // --- Collection/String operators ---

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
        if (right is IEnumerable enumerable and not string)
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

    // --- Type normalization ---

    public static (object? left, object? right) NormalizeTypes(object? left, object? right)
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
        [typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal)];

    private static System.Type WiderNumericType(System.Type a, System.Type b)
    {
        var ai = Array.IndexOf(NumericOrder, a);
        var bi = Array.IndexOf(NumericOrder, b);
        if (ai < 0) ai = NumericOrder.Length - 1;
        if (bi < 0) bi = NumericOrder.Length - 1;
        return NumericOrder[Math.Max(ai, bi)];
    }

    private static object? TryParseNumeric(string s)
    {
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }
}
