using System.Collections;
using App.Errors;
using App.Variables;

namespace App.modules.assert.providers;

public class DefaultAssertProvider : IAssertProvider
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data.@this Equals(Equals action)
    {
        if (AreEqual(action.Expected?.Value, action.Actual?.Value))
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(action.Expected?.Value, action.Actual?.Value, action.Message));
    }

    public Data.@this NotEquals(NotEquals action)
    {
        if (!AreEqual(action.Expected?.Value, action.Actual?.Value))
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(action.Expected?.Value, action.Actual?.Value,
            action.Message ?? "Values should not be equal"));
    }

    public Data.@this IsTrue(IsTrue action)
    {
        if (IsTruthy(action.Value?.Value))
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(true, action.Value?.Value,
            action.Message ?? "Expected truthy value"));
    }

    public Data.@this IsFalse(IsFalse action)
    {
        if (!IsTruthy(action.Value?.Value))
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(false, action.Value?.Value,
            action.Message ?? "Expected falsy value"));
    }

    public Data.@this IsNull(IsNull action)
    {
        if (action.Value?.Value == null)
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(null, action.Value?.Value,
            action.Message ?? "Expected null"));
    }

    public Data.@this IsNotNull(IsNotNull action)
    {
        if (action.Value?.Value != null)
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError("(not null)", null,
            action.Message ?? "Expected non-null value"));
    }

    public Data.@this Contains(Contains action)
    {
        if (ContainsValue(action.Value?.Value, action.Container?.Value))
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(
            FormatValue(action.Container?.Value), action.Value?.Value,
            action.Message ?? "Container does not contain value"));
    }

    public Data.@this GreaterThan(GreaterThan action)
    {
        if (Compare(action.A?.Value, action.B?.Value) > 0)
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(
            $"> {FormatValue(action.B?.Value)}", action.A?.Value,
            action.Message ?? $"Expected {FormatValue(action.A?.Value)} > {FormatValue(action.B?.Value)}"));
    }

    public Data.@this LessThan(LessThan action)
    {
        if (Compare(action.A?.Value, action.B?.Value) < 0)
            return App.Data.@this.Ok(true);

        return App.Data.@this.FromError(new AssertionError(
            $"< {FormatValue(action.B?.Value)}", action.A?.Value,
            action.Message ?? $"Expected {FormatValue(action.A?.Value)} < {FormatValue(action.B?.Value)}"));
    }

    // --- Comparison helpers ---

    private static bool AreEqual(object? expected, object? actual)
    {
        if (ReferenceEquals(expected, actual)) return true;
        if (expected == null || actual == null) return expected == null && actual == null;

        if (IsNumeric(expected) && IsNumeric(actual))
            return Convert.ToDouble(expected) == Convert.ToDouble(actual);

        return expected.Equals(actual) || string.Equals(expected.ToString(), actual.ToString(), StringComparison.Ordinal);
    }

    private static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        if (value is string s) return !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
        if (IsNumeric(value)) return Convert.ToDouble(value) != 0;
        return true;
    }

    private static bool ContainsValue(object? container, object? value)
    {
        if (container == null) return false;

        if (container is string str)
            return str.Contains(value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

        if (container is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
                if (AreEqual(item, value)) return true;
            return false;
        }

        return false;
    }

    private static int Compare(object? a, object? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        if (IsNumeric(a) && IsNumeric(b))
            return Convert.ToDouble(a).CompareTo(Convert.ToDouble(b));

        if (a is IComparable comparable)
            return comparable.CompareTo(b);

        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    private static string FormatValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        return value.ToString() ?? "(null)";
    }

    private static bool IsNumeric(object? value)
        => value is int or long or double or float or decimal
            or byte or short or sbyte or ushort or uint or ulong;
}
