namespace PLang.Tests;

/// <summary>
/// Builds .pr parameter Data the way the builder does — stamping a raw-name slot
/// (e.g. <c>variable.set</c>'s <c>name</c>) with <c>type:variable</c> so
/// <c>type.Judge</c> births a <c>Variable</c> at construction. A hand-built test
/// dict carries no type, so a bare <c>name</c> would otherwise reach the
/// <c>Data&lt;Variable&gt;</c> slot as plain text and decline (CreateDeclined).
/// Mirrors the real <c>.pr</c>, where the builder always stamps these.
/// </summary>
public static class PrParam
{
    public static List<global::app.data.@this> List(
        string module, string action, System.Collections.Generic.IDictionary<string, object?> parameters)
        => parameters.Select(kv => new global::app.data.@this(
                kv.Key, kv.Value,
                IsVarNameSlot(module, action, kv.Key) ? new global::app.type.@this("variable") : null))
            .ToList();

    /// <summary>The (module, action, param) tuples whose slot is a raw-name
    /// <c>Data&lt;Variable&gt;</c>. Kept narrow — extend as tests exercise more.</summary>
    public static bool IsVarNameSlot(string module, string action, string key)
        => string.Equals(module, "variable", System.StringComparison.OrdinalIgnoreCase)
           && string.Equals(action, "set", System.StringComparison.OrdinalIgnoreCase)
           && string.Equals(key, "name", System.StringComparison.OrdinalIgnoreCase);
}
