using System.Collections;
using System.Threading.Tasks;
using app.error;
using app.variable;

namespace app.module.assert.code;

public class Default : IAssert
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // Compares the SCALAR form (access-driven resolution): `assert %x% equals …`
    // is scalar access, so a raw-backed value compares as its raw source form
    // (e.g. config.json untouched is the raw json string), not its materialized
    // shape. For authored/navigated values Peek() == Value, so this is a
    // no-op for everything except untouched raw-backed reads.
    public data.@this<global::app.type.@bool.@this> Equals(Equals action)
    {
        if (AreEqual(action.Expected?.Peek(), action.Actual?.Peek()))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        // Error display keeps .Value (the masked/rendered path); only the
        // comparison uses the scalar form.
        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(action.Expected?.Value, action.Actual?.Value, action.Message?.Value));
    }

    public data.@this<global::app.type.@bool.@this> NotEquals(NotEquals action)
    {
        if (!AreEqual(action.Expected?.Peek(), action.Actual?.Peek()))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(action.Expected?.Value, action.Actual?.Value,
            action.Message?.Value ?? "Values should not be equal"));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> IsTrue(IsTrue action)
    {
        if (await ResolveTruthy(action.Value))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(true, action.Value?.Value,
            action.Message?.Value ?? "Expected truthy value"));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> IsFalse(IsFalse action)
    {
        if (!await ResolveTruthy(action.Value))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(false, action.Value?.Value,
            action.Message?.Value ?? "Expected falsy value"));
    }

    public data.@this<global::app.type.@bool.@this> IsNull(IsNull action)
    {
        if (global::app.type.@null.@this.IsNullValue(action.Value?.Value))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(null, action.Value?.Value,
            action.Message?.Value ?? "Expected null"));
    }

    public data.@this<global::app.type.@bool.@this> IsNotNull(IsNotNull action)
    {
        if (!global::app.type.@null.@this.IsNullValue(action.Value?.Value))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError("(not null)", null,
            action.Message?.Value ?? "Expected non-null value"));
    }

    public data.@this<global::app.type.@bool.@this> Contains(Contains action)
    {
        var v = action.Value?.Value;
        var c = action.Container?.Value;

        // Symmetric containment: the Value/Container names don't match the
        // natural reading "X contains Y" (haystack/needle), so the builder
        // LLM sometimes flips them. Tolerate both orderings — pass if either
        // side contains the other. Both sides must be non-null to avoid
        // string.Contains("") trivially passing on every haystack.
        if (v != null && c != null && (ContainsValue(v, c) || ContainsValue(c, v)))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            FormatValue(c), v,
            action.Message?.Value ?? "Container does not contain value"));
    }

    public data.@this<global::app.type.@bool.@this> NotContains(NotContains action)
    {
        var v = action.Value?.Value;
        var c = action.Container?.Value;

        // Same symmetric tolerance as Contains: fail if either side contains
        // the other (otherwise the builder LLM's Value/Container flip would
        // make this assertion silently pass). If either side is null we
        // can't claim containment, so assertion passes vacuously.
        if (v == null || c == null || (!ContainsValue(v, c) && !ContainsValue(c, v)))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            $"absent: {FormatValue(c)}", v,
            action.Message?.Value ?? "Container contains value but should not"));
    }

    public data.@this<global::app.type.@bool.@this> GreaterThan(GreaterThan action)
    {
        if (Compare(action.A?.Value, action.B?.Value) > 0)
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            $"> {FormatValue(action.B?.Value)}", action.A?.Value,
            action.Message?.Value ?? $"Expected {FormatValue(action.A?.Value)} > {FormatValue(action.B?.Value)}"));
    }

    public data.@this<global::app.type.@bool.@this> LessThan(LessThan action)
    {
        if (Compare(action.A?.Value, action.B?.Value) < 0)
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            $"< {FormatValue(action.B?.Value)}", action.A?.Value,
            action.Message?.Value ?? $"Expected {FormatValue(action.A?.Value)} < {FormatValue(action.B?.Value)}"));
    }

    // --- Comparison helpers ---

    private static bool AreEqual(object? expected, object? actual)
    {
        // Born-native: values arrive as wrappers. Unwrap to the raw backing so a
        // bool.@this and a raw bool compare as bools (true==true), not as strings
        // ("true" vs "True"); a text and a string compare by content.
        if (expected is global::app.type.item.@this ie) expected = ie.ToRaw();
        if (actual is global::app.type.item.@this ia) actual = ia.ToRaw();
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
    /// pipeline uses.
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
        // Born-native: a text container/value rides as text.@this — unwrap so the
        // substring check fires (the wrapper isn't a CLR string). Native dict/list
        // stay as-is (handled by the IEnumerable arm / their own structure).
        if (container is global::app.type.text.@this ct) container = ct.Value;
        if (value is global::app.type.text.@this vt) value = vt.Value;

        if (container is string str)
            return str.Contains(value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

        if (container is app.type.list.@this nl)
        {
            foreach (var item in nl.Items)
                if (AreEqual(item.Value, value)) return true;
            return false;
        }

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
        // Born-native: values arrive as wrappers; unwrap to the raw backing so the
        // numeric/IComparable paths below compare CLR scalars (number→boxed numeric,
        // text→string), not a wrapper a raw int's CompareTo can't handle.
        if (a is global::app.type.item.@this ia) a = ia.ToRaw();
        if (b is global::app.type.item.@this ib) b = ib.ToRaw();
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
