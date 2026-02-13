using System.Collections;

namespace PLang.Runtime2.modules.assert;

/// <summary>
/// Helper for assertion comparisons. Handles type coercion for numeric comparisons.
/// </summary>
internal static class AssertHelper
{
    /// <summary>
    /// Compares two values for equality with numeric type coercion.
    /// </summary>
    internal static bool AreEqual(object? expected, object? actual)
    {
        if (ReferenceEquals(expected, actual)) return true;
        if (expected == null || actual == null) return expected == null && actual == null;

        // Try numeric comparison first (handles int vs double, etc.)
        if (IsNumeric(expected) && IsNumeric(actual))
        {
            var d1 = Convert.ToDouble(expected);
            var d2 = Convert.ToDouble(actual);
            return d1 == d2;
        }

        // String comparison (case-sensitive)
        return expected.Equals(actual) || string.Equals(expected.ToString(), actual.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a value is truthy.
    /// </summary>
    internal static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        if (value is string s) return !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
        if (IsNumeric(value)) return Convert.ToDouble(value) != 0;
        return true;
    }

    /// <summary>
    /// Checks if a container contains a value.
    /// String containers use substring match. Collections use element match.
    /// </summary>
    internal static bool Contains(object? container, object? value)
    {
        if (container == null) return false;

        // String contains
        if (container is string str)
        {
            var search = value?.ToString() ?? "";
            return str.Contains(search, StringComparison.OrdinalIgnoreCase);
        }

        // Collection contains
        if (container is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (AreEqual(item, value))
                    return true;
            }
            return false;
        }

        return false;
    }

    /// <summary>
    /// Compares two values. Returns negative if a &lt; b, 0 if equal, positive if a &gt; b.
    /// </summary>
    internal static int Compare(object? a, object? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return -1;
        if (b == null) return 1;

        if (IsNumeric(a) && IsNumeric(b))
        {
            var d1 = Convert.ToDouble(a);
            var d2 = Convert.ToDouble(b);
            return d1.CompareTo(d2);
        }

        if (a is IComparable comparable)
            return comparable.CompareTo(b);

        return string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal);
    }

    internal static string FormatValue(object? value)
    {
        if (value == null) return "(null)";
        if (value is string s) return $"\"{s}\"";
        return value.ToString() ?? "(null)";
    }

    private static bool IsNumeric(object? value)
    {
        return value is int or long or double or float or decimal
            or byte or short or sbyte or ushort or uint or ulong;
    }
}
