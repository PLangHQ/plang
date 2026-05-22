using System.Collections;
using System.Threading.Tasks;
using app.errors;
using app.variables;

namespace app.modules.assert.code;

public class Default : IAssert
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public data.@this Equals(Equals action)
    {
        if (AreEqual(action.Expected?.Value, action.Actual?.Value))
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(action.Expected?.Value, action.Actual?.Value, action.Message?.Value));
    }

    public data.@this NotEquals(NotEquals action)
    {
        if (!AreEqual(action.Expected?.Value, action.Actual?.Value))
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(action.Expected?.Value, action.Actual?.Value,
            action.Message?.Value ?? "Values should not be equal"));
    }

    public async Task<data.@this> IsTrue(IsTrue action)
    {
        if (await ResolveTruthy(action.Value))
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(true, action.Value?.Value,
            action.Message?.Value ?? "Expected truthy value"));
    }

    public async Task<data.@this> IsFalse(IsFalse action)
    {
        if (!await ResolveTruthy(action.Value))
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(false, action.Value?.Value,
            action.Message?.Value ?? "Expected falsy value"));
    }

    public data.@this IsNull(IsNull action)
    {
        if (action.Value?.Value == null)
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(null, action.Value?.Value,
            action.Message?.Value ?? "Expected null"));
    }

    public data.@this IsNotNull(IsNotNull action)
    {
        if (action.Value?.Value != null)
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError("(not null)", null,
            action.Message?.Value ?? "Expected non-null value"));
    }

    public data.@this Contains(Contains action)
    {
        var v = action.Value?.Value;
        var c = action.Container?.Value;

        // Symmetric containment: the Value/Container names don't match the
        // natural reading "X contains Y" (haystack/needle), so the builder
        // LLM sometimes flips them. Tolerate both orderings — pass if either
        // side contains the other. Both sides must be non-null to avoid
        // string.Contains("") trivially passing on every haystack.
        if (v != null && c != null && (ContainsValue(v, c) || ContainsValue(c, v)))
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(
            FormatValue(c), v,
            action.Message?.Value ?? "Container does not contain value"));
    }

    public data.@this NotContains(NotContains action)
    {
        var v = action.Value?.Value;
        var c = action.Container?.Value;

        // Same symmetric tolerance as Contains: fail if either side contains
        // the other (otherwise the builder LLM's Value/Container flip would
        // make this assertion silently pass). If either side is null we
        // can't claim containment, so assertion passes vacuously.
        if (v == null || c == null || (!ContainsValue(v, c) && !ContainsValue(c, v)))
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(
            $"absent: {FormatValue(c)}", v,
            action.Message?.Value ?? "Container contains value but should not"));
    }

    public data.@this GreaterThan(GreaterThan action)
    {
        if (Compare(action.A?.Value, action.B?.Value) > 0)
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(
            $"> {FormatValue(action.B?.Value)}", action.A?.Value,
            action.Message?.Value ?? $"Expected {FormatValue(action.A?.Value)} > {FormatValue(action.B?.Value)}"));
    }

    public data.@this LessThan(LessThan action)
    {
        if (Compare(action.A?.Value, action.B?.Value) < 0)
            return app.data.@this.Ok(true);

        return app.data.@this.FromError(new AssertionError(
            $"< {FormatValue(action.B?.Value)}", action.A?.Value,
            action.Message?.Value ?? $"Expected {FormatValue(action.A?.Value)} < {FormatValue(action.B?.Value)}"));
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

    /// <summary>
    /// Truthiness of an asserted Data. A value that knows how to answer for
    /// itself (<see cref="app.data.IBooleanResolvable"/> — a path) is routed
    /// through <see cref="data.@this.ToBooleanAsync"/> so the resolvable-dispatch
    /// rule has a single home; everything else falls through to the sync rules.
    /// `assert %path% is true` is thus correct via the same path the condition
    /// pipeline uses. (codeanalyzer v1 F3 / v2 N3)
    /// </summary>
    private static async Task<bool> ResolveTruthy(data.@this? data)
    {
        if (data == null) return false;
        if (data.Value is app.data.IBooleanResolvable)
            return await data.ToBooleanAsync();
        return IsTruthy(data.Value);
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

    private static string FormatValue(object? value) => global::app.Diagnostics.Format.Value(value);

    private static bool IsNumeric(object? value)
        => value is int or long or double or float or decimal
            or byte or short or sbyte or ushort or uint or ulong;
}
