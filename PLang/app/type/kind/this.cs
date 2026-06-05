using System.Collections.Concurrent;
using System.Reflection;

namespace app.type.kind;

/// <summary>
/// Owns the per-type build-time <c>static string? Build(object?)</c> hook
/// — the build-time sibling of <c>Resolve(input, context)</c>. Discovers
/// the hook by reflection, caches the <see cref="MethodInfo"/>, and serves
/// it back via <see cref="Of"/>.
///
/// Why a separate noun: the dispatch + cache is real state with its own
/// invariants (one cache entry per CLR type, computed lazily, never
/// invalidated within an App). Folding it into <c>app.type.catalog.@this</c>
/// would force a verb onto the registry that isn't its concern.
///
/// Pairs with the action-handler <c>IClass.Build()</c> — see
/// <c>Documentation/v0.2/build-vs-runtime.md</c> "Two Builds".
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<System.Type, MethodInfo?> _hookCache = new();

    /// <summary>
    /// Returns the kind <paramref name="clrType"/>'s Build hook produces for
    /// <paramref name="value"/>, or null when the type defines no hook or
    /// the hook returns null. Never throws — a misbehaving hook surfaces
    /// as null (the value just gets no kind).
    /// </summary>
    public string? Of(System.Type? clrType, object? value)
    {
        if (clrType == null) return null;
        var hook = _hookCache.GetOrAdd(clrType, Discover);
        if (hook == null) return null;
        try
        {
            return hook.Invoke(null, new[] { value }) as string;
        }
        catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
        {
            return null;
        }
    }

    private static MethodInfo? Discover(System.Type type)
    {
        var m = type.GetMethod("Build",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
            binder: null,
            types: new[] { typeof(object) },
            modifiers: null);
        if (m == null) return null;
        if (m.ReturnType != typeof(string)) return null;
        return m;
    }
}
