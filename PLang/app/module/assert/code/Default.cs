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

    // Equality through THE comparison entry (data.Compare) — comparison is a
    // USE, so an unread reference parses on its way in (the model rule; no
    // raw-face carve-out). A missing operand equals only a missing operand.
    private static async Task<bool> IsEqual(data.@this? expected, data.@this? actual)
    {
        if (expected == null || actual == null)
            return expected == null && actual == null;
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

    /// <summary>
    /// Truthiness of an asserted Data — one home: the Data's own boolean
    /// dispatch (the instance's IsTruthy; async-resolvable values via
    /// IBooleanResolvable). Same path the condition pipeline uses.
    /// </summary>
    private static async Task<bool> ResolveTruthy(data.@this? data)
        => data != null && await data.ToBooleanAsync();

    // Membership — the ITEM owns the answer (text substring, list element
    // equality through THE comparison entry, dict key, directory listing).
    // A door answer still in raw CLR shape (rung-2 carrier) lifts first.
    private static async Task<bool> ContainsValue(object? container, object? value)
    {
        var c = container as global::app.type.item.@this
            ?? global::app.data.@this.Lift(container);
        if (c == null) return false;
        var needle = value as data.@this ?? new data.@this("", value);
        return await c.Contains(needle);
    }

    private static string FormatValue(object? value) => global::app.Diagnostics.Format.Value(value);
}
