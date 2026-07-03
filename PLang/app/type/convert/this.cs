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
/// Sibling of <c>app.type.kind.Hooks</c> (the build-time <c>Build</c> hook):
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
        => Invoke(_cache, familyClass, value, kind, context);

    // Static cache for the context-free entry point — same discover-once-per-type shape
    // as the instance _cache, but reachable when no App is in scope.
    private static readonly ConcurrentDictionary<System.Type, MethodInfo?> _staticCache = new();

    /// <summary>
    /// Context-free family Convert dispatch — the hook is a <c>static</c> method, so the
    /// scalar families (text/number/bool/datetime/binary/duration) parse a string into
    /// their born-native wrapper without an App in scope. Context-less callers
    /// (the Text serializer's <c>Deserialize&lt;T&gt;(string)</c>) route here; families
    /// that genuinely need a context decline gracefully on the null they receive.
    /// </summary>
    public static global::app.data.@this? OfStatic(System.Type? familyClass, object? value, string? kind,
        actor.context.@this? context)
        => Invoke(_staticCache, familyClass, value, kind, context);

    private static global::app.data.@this? Invoke(ConcurrentDictionary<System.Type, MethodInfo?> cache,
        System.Type? familyClass, object? value, string? kind, actor.context.@this? context)
    {
        if (familyClass == null) return null;
        var hook = cache.GetOrAdd(familyClass, Discover);
        if (hook == null) return null;
        try
        {
            return hook.Invoke(null, new object?[] { value, kind, context }) as global::app.data.@this;
        }
        catch (TargetInvocationException ex)
        {
            // Infra dispatch door: context is legitimately null for context-free callers
            // (the Text serializer's Deserialize<T>), so the error Data is born from the
            // context only when one is in hand.
            var error = new global::app.error.Error(
                (ex.InnerException ?? ex).Message, "TypeConversionFailed", 400);
            return context != null ? context.Error(error) : global::app.data.@this.FromError(error);
        }
    }

    /// <summary>
    /// Routing table: which family owns construction of <paramref name="clrTarget"/>,
    /// plus the number-precision kind when the target pins one. Pure plumbing — it holds
    /// no per-type *logic*, only "who owns this target", and survives a null App so the
    /// infra door works for context-less callers (Data.GetValue, Reconstruct).
    ///
    /// <para><strong>Distributed.</strong> The routing is composed from each family's
    /// <c>static OwnedClrTypes</c> declaration (<see cref="OwnedClr"/>) — the central
    /// <c>if u == typeof(int)…</c> ladder is gone. <c>number</c> declares int/long/…,
    /// <c>text</c> string, <c>datetime</c> DateTimeOffset, <c>duration</c> TimeSpan;
    /// <c>path</c> declares its base <c>Assignable</c> so every scheme subclass routes to
    /// it. Any other type with a <c>Convert</c> hook (goal.call, …) self-owns via
    /// <see cref="Discover"/>. Returns <c>(null, null)</c> when no family owns the target —
    /// the dispatcher then uses its residual leaf + plumbing. Adding an owned CLR type is
    /// an edit to the family's declaration alone.</para>
    /// </summary>
    public static (System.Type? family, string? kind) OwnerOf(System.Type clrTarget)
    {
        var u = System.Nullable.GetUnderlyingType(clrTarget) ?? clrTarget;

        var (exact, assignable) = _ownership.Value;
        if (exact.TryGetValue(u, out var hit)) return hit;
        foreach (var (baseClr, family) in assignable)
            if (baseClr.IsAssignableFrom(u)) return (family, null);
        if (Discover(u) != null) return (u, null);

        return (null, null);
    }

    // CLR → (family, kind) routing, composed once from every family's static
    // OwnedClrTypes declaration. Lazy + cached; survives a null App (pure
    // reflection over the App assembly). Exact matches win; Assignable
    // declarations (path) match any subclass.
    private static readonly System.Lazy<(
        System.Collections.Generic.Dictionary<System.Type, (System.Type? family, string? kind)> Exact,
        System.Collections.Generic.List<(System.Type Base, System.Type Family)> Assignable)>
        _ownership = new(BuildOwnership);

    private static (
        System.Collections.Generic.Dictionary<System.Type, (System.Type? family, string? kind)>,
        System.Collections.Generic.List<(System.Type, System.Type)>) BuildOwnership()
    {
        var exact = new System.Collections.Generic.Dictionary<System.Type, (System.Type? family, string? kind)>();
        var assignable = new System.Collections.Generic.List<(System.Type, System.Type)>();

        foreach (var family in typeof(@this).Assembly.GetTypes())
        {
            // The family's primary class is `@this` (metadata name "this") under app.type.<name>.
            if (!family.Name.Equals("this", System.StringComparison.Ordinal)) continue;
            if (family.Namespace is null || !family.Namespace.StartsWith("app.type.", System.StringComparison.Ordinal)) continue;

            var prop = family.GetProperty("OwnedClrTypes",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (prop?.GetValue(null) is not System.Collections.Generic.IEnumerable<OwnedClr> decls) continue;

            foreach (var decl in decls)
            {
                if (decl.Assignable) assignable.Add((decl.Clr, family));
                else exact[decl.Clr] = (family, decl.Kind);
            }
        }

        return (exact, assignable);
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
