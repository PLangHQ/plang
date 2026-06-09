using System.Collections.Concurrent;
using System.Reflection;

namespace app.type.compare;

/// <summary>
/// Owns the per-type comparison hooks — "the type knows how to rank itself and how
/// to compare two values of its own kind." Two static hooks on the family class
/// (text.@this, number.@this, …), discovered by reflection and cached, exactly the
/// shape of <c>app.type.convert.@this</c>:
///
/// <list type="bullet">
///   <item><c>static int CompareRank</c> — specificity. The higher-ranked type of a
///   pair drives the comparison (number &gt; text, date-family &gt; text, text the
///   floor). A family without the hook ranks at the floor.</item>
///   <item><c>static Comparison Compare(object? a, object? b)</c> — coerces whichever
///   side isn't already of the family's kind into it, then orders/equates in caller
///   order (<c>a</c> is left). Sync — values are already materialised; no I/O.</item>
/// </list>
/// </summary>
public sealed class @this
{
    private static readonly ConcurrentDictionary<System.Type, MethodInfo?> _compareCache = new();
    private static readonly ConcurrentDictionary<System.Type, int> _rankCache = new();

    /// <summary>
    /// Static name → family-class map (app.type.&lt;name&gt;.@this), built once by
    /// reflection over the type namespaces — the comparison hooks are static, so
    /// dispatch needs no App in scope (mirrors convert's OwnerOf). `@`-escaped
    /// folder names (`@bool`, `@null`) map under their bare names.
    /// </summary>
    public static System.Type? FamilyOf(string? name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return _families.Value.TryGetValue(name!, out var t) ? t : null;
    }

    private static readonly System.Lazy<System.Collections.Generic.Dictionary<string, System.Type>> _families
        = new(static () =>
        {
            var map = new System.Collections.Generic.Dictionary<string, System.Type>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var t in typeof(@this).Assembly.GetTypes())
            {
                if (!t.Name.Equals("this", System.StringComparison.Ordinal)) continue;
                var ns = t.Namespace;
                if (ns is null || !ns.StartsWith("app.type.", System.StringComparison.Ordinal)) continue;
                var leaf = ns["app.type.".Length..];
                if (leaf.Contains('.')) continue;        // only direct families, not sub-namespaces
                map[leaf.TrimStart('@')] = t;
            }
            return map;
        });

    /// <summary>The family's static specificity rank; the floor (0) when undeclared.</summary>
    public int RankOf(System.Type? familyClass)
    {
        if (familyClass == null) return 0;
        return _rankCache.GetOrAdd(familyClass, static t =>
        {
            var prop = t.GetProperty("CompareRank",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            return prop?.GetValue(null) is int r ? r : 0;
        });
    }

    /// <summary>
    /// Runs <paramref name="familyClass"/>'s <c>Compare</c> hook on the value pair, in
    /// caller order. Returns <c>null</c> when the family declares no hook — the caller
    /// maps that to <see cref="global::app.data.Comparison.Incomparable"/>.
    /// </summary>
    public global::app.data.Comparison? Of(System.Type? familyClass, object? a, object? b)
    {
        if (familyClass == null) return null;
        var hook = _compareCache.GetOrAdd(familyClass, static t => t.GetMethod("Compare",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
            binder: null,
            types: new[] { typeof(object), typeof(object) },
            modifiers: null) is { } m && m.ReturnType == typeof(global::app.data.Comparison) ? m : null);
        if (hook == null) return null;
        return (global::app.data.Comparison)hook.Invoke(null, new[] { a, b })!;
    }
}
