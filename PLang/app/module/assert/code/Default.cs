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

    public async Task<data.@this<global::app.type.@bool.@this>> Equals(Equals action)
    {
        if (await IsEqual(action.Expected, action.Actual))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        // Error display keeps the materialised form (the masked/rendered path);
        // only the comparison uses the scalar form.
        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(action.Expected?.Peek(), action.Actual?.Peek(), action.Message?.Peek()?.ToString()));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> NotEquals(NotEquals action)
    {
        if (!await IsEqual(action.Expected, action.Actual))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(action.Expected?.Peek(), action.Actual?.Peek(),
            action.Message?.Peek()?.ToString() ?? "Values should not be equal"));
    }

    // Equality through THE comparison entry (data.Compare). One carve-out: an
    // untouched raw-backed operand compares as its SCALAR form (`assert %cfg% equals
    // "{json}"` against the verbatim source text) — data.Compare would force the
    // parse, so the raw rung compares textually instead, preserving lazy reads.
    private static async Task<bool> IsEqual(data.@this? expected, data.@this? actual)
    {
        if (expected == null || actual == null)
            return AreEqual(expected?.Peek(), actual?.Peek());
        if (expected.RawUntouched || actual.RawUntouched)
            return AreEqual(expected.Peek(), actual.Peek());
        return await expected.Compare(actual) == global::app.data.Comparison.Equal;
    }

    public async Task<data.@this<global::app.type.@bool.@this>> IsTrue(IsTrue action)
    {
        if (await ResolveTruthy(action.Value))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(true, (action.Value == null ? null : await action.Value.Value()),
            (action.Message == null ? null : await action.Message.Value())?.ToString() ?? "Expected truthy value"));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> IsFalse(IsFalse action)
    {
        if (!await ResolveTruthy(action.Value))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(false, (action.Value == null ? null : await action.Value.Value()),
            (action.Message == null ? null : await action.Message.Value())?.ToString() ?? "Expected falsy value"));
    }

    public data.@this<global::app.type.@bool.@this> IsNull(IsNull action)
    {
        if (global::app.type.@null.@this.IsNullValue(action.Value?.Peek()))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(null, action.Value?.Peek(),
            action.Message?.Peek()?.ToString() ?? "Expected null"));
    }

    public data.@this<global::app.type.@bool.@this> IsNotNull(IsNotNull action)
    {
        if (!global::app.type.@null.@this.IsNullValue(action.Value?.Peek()))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError("(not null)", null,
            action.Message?.Peek()?.ToString() ?? "Expected non-null value"));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> Contains(Contains action)
    {
        // The door, not Materialize — a reference (file/url) yields its raw
        // content for containment, the scalar contract.
        var v = action.Value == null ? null : await action.Value.Value();
        var c = action.Container == null ? null : await action.Container.Value();

        // Symmetric containment: the Value/Container names don't match the
        // natural reading "X contains Y" (haystack/needle), so the builder
        // LLM sometimes flips them. Tolerate both orderings — pass if either
        // side contains the other. Both sides must be non-null to avoid
        // string.Contains("") trivially passing on every haystack.
        if (v != null && c != null && (await ContainsValue(v, c) || await ContainsValue(c, v)))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            FormatValue(c), v,
            action.Message?.Peek()?.ToString() ?? "Container does not contain value"));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> NotContains(NotContains action)
    {
        var v = action.Value == null ? null : await action.Value.Value();
        var c = action.Container == null ? null : await action.Container.Value();

        // Same symmetric tolerance as Contains: fail if either side contains
        // the other (otherwise the builder LLM's Value/Container flip would
        // make this assertion silently pass). If either side is null we
        // can't claim containment, so assertion passes vacuously.
        if (v == null || c == null || (!await ContainsValue(v, c) && !await ContainsValue(c, v)))
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            $"absent: {FormatValue(c)}", v,
            action.Message?.Peek()?.ToString() ?? "Container contains value but should not"));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> GreaterThan(GreaterThan action)
    {
        if (await Ordered(action.A, action.B) == global::app.data.Comparison.Greater)
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            $"> {FormatValue(action.B?.Peek())}", action.A?.Peek(),
            action.Message?.Peek()?.ToString() ?? $"Expected {FormatValue(action.A?.Peek())} > {FormatValue(action.B?.Peek())}"));
    }

    public async Task<data.@this<global::app.type.@bool.@this>> LessThan(LessThan action)
    {
        if (await Ordered(action.A, action.B) == global::app.data.Comparison.Less)
            return app.data.@this<global::app.type.@bool.@this>.Ok(true);

        return app.data.@this<global::app.type.@bool.@this>.FromError(new AssertionError(
            $"< {FormatValue(action.B?.Peek())}", action.A?.Peek(),
            action.Message?.Peek()?.ToString() ?? $"Expected {FormatValue(action.A?.Peek())} < {FormatValue(action.B?.Peek())}"));
    }

    // Ordering through THE comparison entry. A missing operand never orders
    // (NotEqual — the assert fails with its own message rather than throwing).
    private static async Task<global::app.data.Comparison> Ordered(data.@this? a, data.@this? b)
    {
        if (a == null || b == null) return global::app.data.Comparison.NotEqual;
        return await a.Compare(b);
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
        if ((await data.Value()) is app.data.IBooleanResolvable)
            return await data.ToBooleanAsync();
        return IsTruthy((await data.Value()));
    }

    private static bool IsTruthy(object? value)
    {
        if (value == null) return false;
        if (value is bool b) return b;
        if (value is string s) return !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase);
        if (IsNumeric(value)) return Convert.ToDouble(value) != 0;
        return true;
    }

    // Membership through THE comparison entry: matches only on Equal and never
    // errors — NotEqual/Incomparable mean "not this one", so a mixed container
    // can't blow an assert contains.
    private static async Task<bool> ContainsValue(object? container, object? value)
    {
        if (container == null) return false;
        // Born-native: a text container/value rides as text.@this — unwrap so the
        // substring check fires (the wrapper isn't a CLR string). Native dict/list
        // stay as-is (handled by the IEnumerable arm / their own structure).
        if (container is global::app.type.text.@this ct) container = ct.Value;
        var substringNeedle = value is global::app.type.text.@this vt ? vt.Value : value?.ToString();

        if (container is string str)
            return str.Contains(substringNeedle ?? "", StringComparison.OrdinalIgnoreCase);

        var target = value as data.@this ?? new data.@this("", value);
        if (container is app.type.list.@this nl)
        {
            foreach (var item in nl.Items)
                if (await item.Compare(target) == global::app.data.Comparison.Equal) return true;
            return false;
        }

        if (container is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                var element = item as data.@this ?? new data.@this("", item);
                if (await element.Compare(target) == global::app.data.Comparison.Equal) return true;
            }
            return false;
        }

        // A directory's membership is over its listing (the type owns it).
        if (container is global::app.type.directory.@this dirVal)
            return await dirVal.Contains(substringNeedle ?? "");

        // A scalar with an honest text form (a path's location, a number)
        // contains by substring — mirrors text's coercion rules. Containers
        // were handled above; a dict has no honest text form.
        if (container is not global::app.type.dict.@this and not data.@this and not System.Collections.IDictionary)
        {
            var text = container.ToString();
            if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(substringNeedle))
                return text.Contains(substringNeedle, StringComparison.OrdinalIgnoreCase);
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
