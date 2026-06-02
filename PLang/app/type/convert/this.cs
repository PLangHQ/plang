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

    /// <summary>
    /// Routing table: which family owns construction of <paramref name="clrTarget"/>,
    /// plus the number-precision kind when the target pins one. Pure plumbing — it holds
    /// no per-type *logic*, only "who owns this target", and survives a null App so the
    /// infra door works for context-less callers (Data.GetValue, Reconstruct).
    ///
    /// <para>Raw CLR primitives a family constructs (int/long/decimal/double/float →
    /// <c>number</c>, string → <c>text</c>, DateTimeOffset → <c>datetime</c>, TimeSpan →
    /// <c>duration</c>) map to that family. A path subclass maps to <c>path</c>. Any other
    /// type that declares its own <c>Convert</c> hook (image, goal.call, …) owns itself.
    /// Returns <c>(null, null)</c> when no family owns the target — the dispatcher then
    /// uses its residual leaf + plumbing.</para>
    /// </summary>
    public static (System.Type? family, string? kind) OwnerOf(System.Type clrTarget)
    {
        var u = System.Nullable.GetUnderlyingType(clrTarget) ?? clrTarget;

        if (u == typeof(int)) return (typeof(global::app.type.number.@this), "int");
        if (u == typeof(long)) return (typeof(global::app.type.number.@this), "long");
        if (u == typeof(decimal)) return (typeof(global::app.type.number.@this), "decimal");
        if (u == typeof(double)) return (typeof(global::app.type.number.@this), "double");
        if (u == typeof(float)) return (typeof(global::app.type.number.@this), "float");
        if (u == typeof(string)) return (typeof(global::app.type.text.@this), null);
        if (u == typeof(System.DateTimeOffset)) return (typeof(global::app.type.datetime.@this), null);
        if (u == typeof(System.TimeSpan)) return (typeof(global::app.type.duration.@this), null);
        if (typeof(global::app.type.path.@this).IsAssignableFrom(u)) return (typeof(global::app.type.path.@this), null);
        if (Discover(u) != null) return (u, null);

        return (null, null);
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
