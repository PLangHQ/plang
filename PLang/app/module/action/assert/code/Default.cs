using System.Collections;
using System.Threading.Tasks;
using app.error;
using app.variable;

namespace app.module.action.assert.code;

public class Default : IAssert
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Equals(Equals action)
    {
        if (await IsEqual(action.Expected, action.Actual))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        // Error display keeps the materialised form (the masked/rendered path);
        // only the comparison uses the scalar form.
        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(action.Expected?.Peek(), action.Actual?.Peek(), action.Message?.Peek()?.ToString()));
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> NotEquals(NotEquals action)
    {
        if (!await IsEqual(action.Expected, action.Actual))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(action.Expected?.Peek(), action.Actual?.Peek(),
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

    public async Task<data.@this<global::app.type.item.@bool.@this>> IsTrue(IsTrue action)
    {
        if (await ResolveTruthy(action.Value))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(true, (action.Value == null ? null : await action.Value.Value()),
            (action.Message == null ? null : await action.Message.Value())?.ToString() ?? "Expected truthy value"));
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> IsFalse(IsFalse action)
    {
        if (!await ResolveTruthy(action.Value))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(false, (action.Value == null ? null : await action.Value.Value()),
            (action.Message == null ? null : await action.Message.Value())?.ToString() ?? "Expected falsy value"));
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> IsNull(IsNull action)
    {
        // RESOLVE the value (open its door), don't Peek the raw form — a %ref%/template Peeks to its
        // unrendered source (the name / the literal) and would never read as null. Same door
        // IsTrue/IsFalse/Contains use. An UNSET %var% is absent → null for a null-check (not an error).
        var value = await ResolveOrNull(action.Value);
        if (global::app.type.item.@null.@this.IsNullValue(value))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(null, value,
            action.Message?.Peek()?.ToString() ?? "Expected null"));
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> IsNotNull(IsNotNull action)
    {
        var value = await ResolveOrNull(action.Value);
        if (!global::app.type.item.@null.@this.IsNullValue(value))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError("(not null)", null,
            action.Message?.Peek()?.ToString() ?? "Expected non-null value"));
    }

    // Resolve a value for a null-check: open its door, but treat an unset %var% as absent (null)
    // rather than a hard read error — "is %shared% null" is exactly the question you ask BEFORE
    // knowing whether the variable was ever set.
    private static async Task<global::app.type.item.@this?> ResolveOrNull(data.@this? value)
    {
        if (value == null) return null;
        try { return await value.Value(); }
        catch (global::app.error.VariableNotFoundException) { return null; }
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Contains(Contains action)
    {
        // The door, not Materialize — a reference (file/url) yields its raw
        // content for containment, the scalar contract.
        var vItem = action.Value == null ? null : await action.Value.Value();
        var cItem = action.Container == null ? null : await action.Container.Value();

        // Symmetric containment: the Value/Container names don't match the
        // natural reading "X contains Y" (haystack/needle), so the builder
        // LLM sometimes flips them. Tolerate both orderings — pass if either
        // side contains the other. The item is the haystack; the needle is the
        // ORIGINAL context-ful Data (action.Value / action.Container), never a
        // fresh re-wrap. Both sides must be non-null to avoid string.Contains("")
        // trivially passing on every haystack.
        if (vItem != null && cItem != null
            && (await vItem.Contains(action.Container!) || await cItem.Contains(action.Value!)))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(
            FormatValue(cItem), vItem,
            action.Message?.Peek()?.ToString() ?? "Container does not contain value"));
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> NotContains(NotContains action)
    {
        var vItem = action.Value == null ? null : await action.Value.Value();
        var cItem = action.Container == null ? null : await action.Container.Value();

        // Same symmetric tolerance as Contains: fail if either side contains
        // the other (otherwise the builder LLM's Value/Container flip would
        // make this assertion silently pass). If either side is null we
        // can't claim containment, so assertion passes vacuously.
        if (vItem == null || cItem == null
            || (!await vItem.Contains(action.Container!) && !await cItem.Contains(action.Value!)))
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(
            $"absent: {FormatValue(cItem)}", vItem,
            action.Message?.Peek()?.ToString() ?? "Container contains value but should not"));
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> GreaterThan(GreaterThan action)
    {
        if (await Ordered(action.A, action.B) == global::app.data.Comparison.Greater)
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(
            $"> {FormatValue(action.B?.Peek())}", action.A?.Peek(),
            action.Message?.Peek()?.ToString() ?? $"Expected {FormatValue(action.A?.Peek())} > {FormatValue(action.B?.Peek())}"));
    }

    public async Task<data.@this<global::app.type.item.@bool.@this>> LessThan(LessThan action)
    {
        if (await Ordered(action.A, action.B) == global::app.data.Comparison.Less)
            return action.Context.Ok<global::app.type.item.@bool.@this>(true);

        return action.Context.Error<global::app.type.item.@bool.@this>(new AssertionError(
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

    private static string FormatValue(object? value) => global::app.Diagnostics.Format.Value(value);
}
