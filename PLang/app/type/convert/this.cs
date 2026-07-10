using System.Collections.Concurrent;
using System.Reflection;
using app.actor.context;

namespace app.type.convert;

/// <summary>
/// Owns the CLR → owning-family routing (<see cref="OwnerOf"/>), composed from each family's
/// static <c>OwnedClrTypes</c> declaration. The per-type <c>Convert</c> hook dispatch it once
/// carried is gone — a type builds itself through its own <c>Create</c> (the entity courier);
/// this stays only as the clr-target routing table the residual conversion plumbing consults.
/// </summary>
public sealed class @this
{
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
