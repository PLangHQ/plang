using app.Errors;

namespace app.modules.assert;

/// <summary>
/// Shared helper used by all assert handlers on failure: attaches the current
/// Variables snapshot to the AssertionError so the runner can render it in the
/// failure diagnostic. No-op on success (guard from architect §4.6) and when the
/// error is not an AssertionError (e.g. provider returned a different error type).
/// </summary>
internal static class AssertSnapshot
{
    public static Data.@this WithVariables(Data.@this result, Actor.Context.@this context)
    {
        if (result.Success) return result;
        if (result.Error is AssertionError err && err.Variables == null)
            err.Variables = context.Variables.Snapshot();
        return result;
    }
}
