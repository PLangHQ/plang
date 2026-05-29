using app.error;

namespace app.module.assert;

/// <summary>
/// Shared helper used by all assert handlers on failure: attaches the current
/// Variables snapshot to the AssertionError so the runner can render it in the
/// failure diagnostic. No-op on success (guard from architect §4.6) and when the
/// error is not an AssertionError (e.g. provider returned a different error type).
/// Generic so it preserves the typed return — every assert action's Run() returns
/// <c>Data&lt;bool&gt;</c>.
/// </summary>
internal static class AssertSnapshot
{
    public static data.@this<T> WithVariables<T>(data.@this<T> result, actor.context.@this context)
    {
        if (result.Success) return result;
        if (result.Error is AssertionError err && err.Variables == null)
            err.Variables = context.Variable.Snapshot();
        return result;
    }
}
