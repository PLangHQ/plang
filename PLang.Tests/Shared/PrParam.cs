using System.Reflection;

namespace PLang.Tests;

/// <summary>
/// Builds .pr parameter Data the way the builder does — stamping a raw-name slot
/// (e.g. <c>variable.set</c>'s <c>name</c>) with <c>type:variable</c> so
/// <c>type.Judge</c> births a <c>Variable</c> at construction. A hand-built test
/// dict carries no type, so a bare <c>name</c> would otherwise reach the
/// <c>Data&lt;Variable&gt;</c> slot as plain text and decline (CreateVariableDeclined).
/// Mirrors the real <c>.pr</c>, where the builder always stamps these.
/// </summary>
public static class PrParam
{
    public static List<global::app.data.@this> List(
        string module, string action, System.Collections.Generic.IDictionary<string, object?> parameters)
        => parameters.Select(kv => new global::app.data.@this(
                kv.Key, kv.Value,
                IsVarNameSlot(module, action, kv.Key) ? new global::app.type.@this("variable") : null,
                context: global::PLang.Tests.TestApp.SharedContext))
            .ToList();

    /// <summary>
    /// True when the action handler declares this parameter as a name slot —
    /// <c>Data&lt;variable&gt;</c>. Read straight off the handler's property
    /// declaration, not a hand-maintained list, so every list/loop/variable name
    /// slot is covered automatically and stays correct as actions change.
    /// </summary>
    public static bool IsVarNameSlot(string module, string action, string key)
    {
        var handler = FindHandler(module, action);
        var prop = handler?.GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop == null) return false;

        var t = System.Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(global::app.data.@this<>)) return false;

        return t.GetGenericArguments()[0] == typeof(global::app.variable.@this);
    }

    // (module, action) → handler type, discovered once by reflecting [Action] over
    // the PLang assembly. Action name defaults to the class name when unset.
    private static readonly Dictionary<(string, string), System.Type> _handlers = BuildHandlerMap();

    private static System.Type? FindHandler(string module, string action)
        => _handlers.TryGetValue((Lc(module), Lc(action)), out var t) ? t : null;

    private static Dictionary<(string, string), System.Type> BuildHandlerMap()
    {
        var map = new Dictionary<(string, string), System.Type>();
        foreach (var t in typeof(global::app.module.ActionAttribute).Assembly.GetTypes())
        {
            var attr = t.GetCustomAttribute<global::app.module.ActionAttribute>();
            if (attr == null || t.Namespace == null || !t.Namespace.StartsWith("app.module.")) continue;
            var module = t.Namespace["app.module.".Length..];
            map[(Lc(module), Lc(attr.Name ?? t.Name))] = t;
        }
        return map;
    }

    private static string Lc(string s) => s.ToLowerInvariant();
}
