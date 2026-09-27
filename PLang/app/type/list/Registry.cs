using System.Reflection;
using app.Attributes;

namespace app.type.list;

/// <summary>
/// Registry partial of <see cref="@this"/> — the one set of types: how a type comes in
/// (<see cref="Add(System.Type, actor.context.@this, string?)"/>, <see cref="Add(Assembly, actor.context.@this)"/>),
/// the startup scan, and the name a C# class reports.
///
/// A class's name, in order:
///   1. [PlangType("name")] on the class — the declared name; [PlangType] with no name infers it.
///   2. An @this class — the last namespace segment.
///   3. Only items (app.type.item.@this) are plang types; an engine class has no type name.
///   4. A path scheme or a typed program list (<c>list&lt;step&gt;</c>) is a kind of its family and
///      claims no name of its own.
///   One name, one class.
/// </summary>
public sealed partial class @this
{
    private readonly object _lock = new();
    private volatile bool _loaded;

    // The types, in the order they came in. Copy-on-write: a walk reads the array it holds, and an
    // Add swaps in a new one, so a lookup never sees a half-added type.
    private volatile global::app.type.@this[] _types = [];

    /// <summary>Assemblies the startup scan reads. Defaults to the App assembly; callers can extend.</summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    /// <summary>
    /// Built-in type names a runtime-loaded assembly may not claim. Their bodies are signing- or
    /// transport-load-bearing: a DLL that replaced <c>identity</c>'s class or its renderer could
    /// produce authentically-signed values whose body was attacker-composed.
    /// </summary>
    public IReadOnlySet<string> Sealed { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "identity", "signature", "signedoperation", "callback", "channel",
    };

    /// <summary>
    /// The reserved core of the navigation planes — <c>%x!type%</c>/<c>!error%</c>/<c>!success%</c>
    /// and the <c>@schema</c> wire marker always answer from the Data. A value type may not declare
    /// an instance property under these names: it would shadow (or be shadowed by) the Data on the
    /// <c>!</c> plane. Statics are fine; only the navigable instance surface can shadow.
    /// </summary>
    public IReadOnlySet<string> Reserved { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "type", "error", "success", "@schema",
    };

    // Every type, loaded on first ask.
    private global::app.type.@this[] Types
    {
        get
        {
            Load();
            return _types;
        }
    }

    // The startup scan: every item class in Assemblies comes in under its name, then each gets its
    // facts once every name is known (a fact names its property types). A clash here is plang's own.
    private void Load()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            var named = new List<global::app.type.@this>();
            foreach (var assembly in Assemblies)
                foreach (var clr in SafeGetTypes(assembly))
                    if (NameOf(clr) is { } name)
                    {
                        if (named.Find(t => t.Names(name)) is { } owner)
                            throw new InvalidOperationException(
                                $"type name '{name}' is claimed by both {owner.ClrType!.FullName} and {clr.FullName} — one name, one class.");
                        named.Add(new global::app.type.@this(name, clr, null));
                    }
            _types = [.. named];
            _types = [.. named.Select(t => new global::app.type.@this(t.Name, t.ClrType!, this))];
            _loaded = true;
        }
    }

    /// <summary>
    /// Adds the plang type <paramref name="clr"/> defines — under <paramref name="name"/>, or the
    /// name the class reports. A name another class owns, a sealed name, or an instance property
    /// under a reserved name is an error; the set is unchanged.
    /// </summary>
    public data.@this Add(System.Type clr, actor.context.@this context, string? name = null)
    {
        if ((name ?? NameOf(clr)) is not { } claimed)
            return context.Error(new error.Error($"{clr.FullName} is not a plang type — only items are.", "TypeLoadNotAType", 400));
        if (Sealed.Contains(claimed))
            return context.Error(new error.Error(
                $"'{claimed}' is on the sealed built-in list and may not be claimed by a runtime-loaded type.", "TypeLoadCollision", 400));
        if (Array.Find(clr.GetProperties(BindingFlags.Public | BindingFlags.Instance), p => Reserved.Contains(p.Name)) is { } shadow)
            return context.Error(new error.Error(
                $"Type '{clr.FullName}' declares instance property '{shadow.Name}' — `type`/`error`/`success`/`@schema` are the reserved navigation core and may not be shadowed by a value type.",
                "TypeLoadReservedShadow", 400));
        lock (_lock)
        {
            Load();
            if (Array.Find(_types, t => t.Names(claimed)) is { } owner)
                return owner.ClrType == clr
                    ? context.Ok(owner)
                    : context.Error(new error.Error(
                        $"type name '{claimed}' is claimed by both {owner.ClrType?.FullName} and {clr.FullName} — one name, one class.",
                        "TypeLoadCollision", 400));
            // Its own name first, so its facts can name a property of its own type.
            _types = [.. _types, new global::app.type.@this(claimed, clr, null)];
            var added = new global::app.type.@this(claimed, clr, this);
            _types = [.. _types[..^1], added];
            return context.Ok(added);
        }
    }

    /// <summary>
    /// Adds every plang type <paramref name="assembly"/> exports, and registers its renderers
    /// (<see cref="ITypeRenderer"/>). Every type added must render — its own renderer or one already
    /// known. The first error stops the load; the types added before it stay.
    /// </summary>
    public data.@this Add(Assembly assembly, actor.context.@this context)
    {
        System.Type[] exported;
        try { exported = assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex) { exported = ex.Types.Where(t => t != null).ToArray()!; }

        var added = new List<global::app.type.@this>();
        foreach (var clr in exported)
        {
            if (clr.IsAbstract || clr.IsInterface) continue;
            // A class that claims a sealed name is refused whether or not it is an item: the name is
            // what a loaded assembly may not take.
            var claims = clr.GetCustomAttribute<PlangTypeAttribute>(inherit: false) is { } declared
                ? declared.Name ?? InferName(clr)
                : IsThisClass(clr) ? InferName(clr) : null;
            if (claims != null && Sealed.Contains(claims))
                return context.Error(new error.Error(
                    $"'{claims}' is on the sealed built-in list and may not be claimed by a runtime-loaded type ({clr.FullName}).", "TypeLoadCollision", 400));
            if (NameOf(clr) is null) continue;
            var result = Add(clr, context);
            if (!result.Success) return result;
            added.Add(this[NameOf(clr)!]);
        }

        foreach (var clr in exported)
        {
            if (clr.IsAbstract || clr.IsInterface || !typeof(ITypeRenderer).IsAssignableFrom(clr)) continue;
            if (clr.GetConstructor(System.Type.EmptyTypes) is not { } ctor) continue;
            var renderer = (ITypeRenderer)ctor.Invoke(null);
            if (Sealed.Contains(renderer.TypeName))
                return context.Error(new error.Error(
                    $"ITypeRenderer for '{renderer.TypeName}' rejected — '{renderer.TypeName}' is on the sealed built-in list and its rendering may not be replaced by a runtime-loaded DLL.",
                    "TypeLoadCollision", 400));
            Renderer.Register(renderer.TypeName, renderer.Format, (value, writer) => renderer.Write(value, writer));
        }

        if (added.Find(t => !Renderer.Has(t.Name)) is { } bare)
            return context.Error(new error.Error(
                $"type '{bare.Name}' loaded with no covering renderer (need a Default ITypeRenderer or per-format coverage).",
                "TypeLoadCoverage", 400));
        return context.Ok(added);
    }

    // The name a class reports as a type of its own, or null: not an item, a kind of a family, or an
    // abstract class that neither declares a name nor is an @this.
    private static string? NameOf(System.Type type)
    {
        if (!typeof(app.type.item.@this).IsAssignableFrom(type) || FamilyName(type) != null) return null;
        var declared = type.GetCustomAttribute<PlangTypeAttribute>(inherit: false);
        if (declared != null) return declared.Name ?? InferName(type);
        if (!IsThisClass(type) || type.ContainsGenericParameters) return null;
        return InferName(type);
    }

    private static bool IsThisClass(System.Type type) =>
        string.Equals(type.Name, "this", StringComparison.Ordinal);

    /// <summary>
    /// The family a class is a KIND of — only when it says so. A path scheme declares it
    /// (<c>[PathScheme("file")]</c> → {path, kind: file}); a typed program list is one through its
    /// generic base (<c>list&lt;step&gt;</c> → {list, kind: step}). Null for every other class:
    /// by default a class's name is its own (modifier is "modifier", never a kind of action).
    /// </summary>
    private static string? FamilyName(System.Type type)
    {
        if (type.IsDefined(typeof(app.type.item.path.PathSchemeAttribute), inherit: false))
            return InferName(typeof(app.type.item.path.@this));
        for (var b = type.BaseType; b != null; b = b.BaseType)
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(app.type.item.list.@this<>))
                return InferName(typeof(app.type.item.list.@this));
        return null;
    }

    /// <summary>
    /// Inferred name: last namespace segment for @this classes, lowercased class
    /// name otherwise. Null when the type has no namespace.
    /// </summary>
    private static string? InferName(System.Type type)
    {
        if (IsThisClass(type))
        {
            if (string.IsNullOrEmpty(type.Namespace)) return null;
            var ns = type.Namespace;
            var lastDot = ns.LastIndexOf('.');
            return (lastDot >= 0 ? ns[(lastDot + 1)..] : ns).ToLowerInvariant();
        }
        return type.Name.ToLowerInvariant();
    }

    private static IEnumerable<System.Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}
