namespace PLang.Runtime2.modules.math;

/// <summary>
/// Helper for numeric operations that preserves type when possible.
/// </summary>
internal static class MathHelper
{
    /// <summary>
    /// Convert value to double for math operations.
    /// </summary>
    internal static double ToDouble(object? value)
    {
        if (value == null) return 0;
        return Convert.ToDouble(value);
    }

    /// <summary>
    /// Returns the result in the "widest" type of the two inputs.
    /// int + int = int, int + double = double, etc.
    /// </summary>
    internal static object PreserveType(double result, object? a, object? b)
    {
        // If both inputs are integers and result fits, keep as integer
        if (IsIntegral(a) && IsIntegral(b))
        {
            if (result == Math.Truncate(result) && result >= long.MinValue && result <= long.MaxValue)
            {
                var longResult = (long)result;

                // If both inputs were int, return int if fits
                if (a is int or byte or short && b is int or byte or short)
                {
                    if (longResult >= int.MinValue && longResult <= int.MaxValue)
                        return (int)longResult;
                }

                return longResult;
            }
        }

        // If any input was decimal, return decimal
        if (a is decimal || b is decimal)
            return (decimal)result;

        // If any input was float (not double), return float
        if (a is float && b is not double)
            return (float)result;

        return result;
    }

    /// <summary>
    /// Returns the result preserving the type of a single input.
    /// </summary>
    internal static object PreserveType(double result, object? input)
    {
        return PreserveType(result, input, input);
    }

    private static bool IsIntegral(object? value)
    {
        return value is int or long or byte or short or sbyte or ushort or uint or ulong;
    }
}
