using System.Collections.Concurrent;
using System.Reflection;
using app.actor.context;

namespace app.type.convert;

/// <summary>
/// Owns the per-type <c>static data.@this Convert(object?, string? kind, context)</c>
/// hook — "the type knows how to make a value of itself, kind-aware." Discovers
/// the hook by reflection on the family class (text.@this, number.@this, …),
/// caches the <see cref="MethodInfo"/>, and invokes it via <see cref="Of"/>.
///
/// Sibling of <c>app.type.kind.@this</c> (the build-time <c>Build</c> hook):
/// same discover-cache-invoke shape, different verb. Kept a separate noun because
/// the dispatch + cache is real state with its own invariants (one entry per CLR
/// type, lazy, never invalidated within an App).
/// </summary>
public sealed class @this
{
    private readonly ConcurrentDictionary<System.Type, MethodInfo?> _cache = new();

    /// <summary>
    /// Asks <paramref name="familyClass"/>'s <c>Convert</c> hook to make a value of
    /// that type from <paramref name="value"/> (with <paramref name="kind"/>). Returns
    /// the type's result — which may itself be an Error Data — or <c>null</c> when the
    /// family defines no <c>Convert</c> hook (the caller then falls back).
    /// </summary>
    public global::app.data.@this? Of(System.Type? familyClass, object? value, string? kind,
        actor.context.@this context)
    {
        if (familyClass == null) return null;
        var hook = _cache.GetOrAdd(familyClass, Discover);
        if (hook == null) return null;
        try
        {
            return hook.Invoke(null, new object?[] { value, kind, context }) as global::app.data.@this;
        }
        catch (TargetInvocationException ex)
        {
            return global::app.data.@this.FromError(new global::app.error.Error(
                (ex.InnerException ?? ex).Message, "TypeConversionFailed", 400));
        }
    }

    private static MethodInfo? Discover(System.Type type)
    {
        var m = type.GetMethod("Convert",
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy,
            binder: null,
            types: new[] { typeof(object), typeof(string), typeof(actor.context.@this) },
            modifiers: null);
        if (m == null) return null;
        if (!typeof(global::app.data.@this).IsAssignableFrom(m.ReturnType)) return null;
        return m;
    }
}
