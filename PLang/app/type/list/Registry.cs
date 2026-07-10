using System.Collections.Concurrent;
using System.Reflection;
using app.Attributes;

namespace app.type.list;

/// <summary>
/// Registry partial of <see cref="@this"/> — absorbs the former <c>PlangTypeIndex</c>
/// (single source of truth for domain type identity).
///
/// Rules, in order:
///   1. [PlangType("name")] on the class — declared name wins. Multiple
///      [PlangType] attributes act as aliases; the first non-null Name is canonical.
///   2. [PlangType] with no Name — inferred name (@this convention: last
///      namespace segment; otherwise class name lowercased).
///   3. @this classes WITHOUT [PlangType] — last-namespace-segment is the concept
///      name (type→name). Only those that inherit app.type.item.@this (PLang
///      values) also claim the forward name→type slot; engine mechanics (e.g.
///      app.variable.path) report a concept name but are not resolvable as types,
///      so they cannot shadow real value types.
///   4. Otherwise: type has no PLang name and is opaque.
/// </summary>
public sealed partial class @this
{
    private readonly object _initLock = new();
    private bool _initialized;
    private readonly ConcurrentDictionary<string, Type> _nameToType = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Type, string> _typeToName = new();
    // Runtime registrations (test harnesses, plugins) merge into the index.
    private readonly ConcurrentDictionary<string, Type> _runtimeNameToType = new(StringComparer.OrdinalIgnoreCase);

    // The clr → owning-plang-name index (int→"number", DateOnly→"date"), populated inline as each
    // value type is indexed, from its own OwnedClrTypes declaration. Feeds the born-native lift's clr
    // rung: a raw CLR scalar resolves to the entity that owns its shape. Mutable like its sibling
    // indices — a code.load type adds its ownership at runtime. Exact keys only; the one Assignable
    // declaration (path) is always an item.@this and never a raw CLR value.
    private readonly ConcurrentDictionary<Type, string> _clr = new();

    private readonly HashSet<string> _clrTypeFullNames = new(StringComparer.Ordinal);
    private volatile bool _clrTypeFullNamesInitialized;
    private readonly object _clrTypeFullNamesLock = new();

    /// <summary>Assemblies to scan for [PlangType] discovery. Defaults to the App assembly; callers can extend.</summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    /// <summary>
    /// True if <paramref name="name"/> matches the FullName of any type in any loaded assembly.
    /// Used to defend goal-name slots against CLR-type-name leaks (a known builder bug:
    /// e.g. <c>app.goal.GoalCall</c> getting written as a goal Name during prompt rendering).
    /// A goal Name is a user-authored identifier — it can never legitimately equal a CLR type name.
    /// </summary>
    public bool IsClrTypeName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (!name.Contains('.')) return false;
        EnsureClrTypeFullNamesInitialized();
        return _clrTypeFullNames.Contains(name);
    }

    private void EnsureClrTypeFullNamesInitialized()
    {
        if (_clrTypeFullNamesInitialized) return;
        lock (_clrTypeFullNamesLock)
        {
            if (_clrTypeFullNamesInitialized) return;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in SafeGetTypes(asm))
                {
                    if (t.FullName != null) _clrTypeFullNames.Add(t.FullName);
                }
            }
            _clrTypeFullNamesInitialized = true;
        }
    }

    /// <summary>
    /// Returns the CLR type for a PLang name, or null if no type is registered
    /// under that name.
    /// </summary>
    public Type? ResolveType(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        EnsureInitialized();
        if (_runtimeNameToType.TryGetValue(name, out var runtime)) return runtime;
        return _nameToType.TryGetValue(name, out var t) ? t : null;
    }

    /// <summary>
    /// All types known to the index, deduplicated by CLR type. Useful for seeding
    /// a catalog build that isn't started from action parameters.
    /// </summary>
    public IEnumerable<Type> KnownTypes()
    {
        EnsureInitialized();
        return _typeToName.Keys.Concat(_runtimeNameToType.Values).Distinct();
    }

    /// <summary>
    /// Registers a name → type mapping at runtime. Prefer [PlangType] on the class.
    /// This is for synthetic/test types that can't carry attributes.
    /// </summary>
    public void RegisterRuntime(string name, Type type)
    {
        if (string.IsNullOrWhiteSpace(name) || type == null) return;
        _runtimeNameToType[name] = type;
        _typeToName.TryAdd(type, name);
        // A code.load type owns its CLR shapes at runtime too — add them to the clr index.
        if (type.GetProperty("OwnedClrTypes", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                ?.GetValue(null) is IEnumerable<global::app.type.convert.OwnedClr> owned)
            foreach (var decl in owned)
                if (!decl.Assignable) _clr.TryAdd(decl.Clr, name);
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            SeedClrPrimitives();
            // An explicitly-aliased [PlangType] name (one that diverges from the
            // type's own inferred name — a kind label that redirects to a name the
            // type's namespace/class does not itself carry) must not steal the
            // name→type resolve slot from a type that naturally owns that name. So the
            // type→name (kind) mapping records immediately, but the alias's name→type
            // claim is deferred to a second pass that runs after every natural owner
            // has registered. The kind direction is collision-free (keyed by type);
            // only the reverse needs the ordering guarantee.
            var deferredAliases = new List<(string Name, Type Type)>();
            foreach (var assembly in Assemblies)
                IndexAssembly(assembly, deferredAliases);
            foreach (var (name, type) in deferredAliases)
                _nameToType.TryAdd(name, type);
            _initialized = true;
        }
    }

    /// <summary>
    /// Seeds CLR-primitive PLang names (<c>string</c>, <c>int</c>, …) into the
    /// registry from <c>app.type.app.type.primitive.@this</c> — the single source for
    /// the seeded data. These types have no folder, no <c>Resolve</c>, no
    /// <c>Build</c>; the registration only wires name↔CLR type so
    /// <see cref="ResolveType"/> stays the one lookup path.
    /// </summary>
    private void SeedClrPrimitives()
    {
        foreach (var (name, type) in app.type.primitive.@this.Aliases)
            _nameToType.TryAdd(name, type);
        foreach (var (type, name) in app.type.primitive.@this.Canonical)
            _typeToName.TryAdd(type, name);
    }


    private void IndexAssembly(Assembly assembly, List<(string Name, Type Type)> deferredAliases)
    {
        foreach (var type in SafeGetTypes(assembly))
        {
            var attrs = type.GetCustomAttributes<PlangTypeAttribute>(inherit: false).ToList();

            // Skip abstract (non-static) types UNLESS they declare [PlangType] OR
            // are an @this class — an abstract @this is the base of a scheme/family
            // (e.g. path.@this with concrete subclasses FilePath, HttpPath). The
            // PLang name resolves to the base; construction dispatches via a
            // registry (Scheme.From).
            if (type.IsAbstract && !type.IsSealed && attrs.Count == 0 && !IsThisClass(type)) continue;
            string? canonical = null;

            if (attrs.Count > 0)
            {
                var inferred = InferName(type);
                foreach (var attr in attrs)
                {
                    var name = attr.Name ?? inferred;
                    if (name == null) continue;
                    // A name that matches the type's own inference is its natural
                    // claim — register now. A name the attribute redirects to
                    // (kind label that diverges from the type's namespace/class)
                    // is deferred so a natural owner of that name wins the reverse.
                    if (string.Equals(name, inferred, System.StringComparison.Ordinal))
                        _nameToType.TryAdd(name, type);
                    else
                        deferredAliases.Add((name, type));
                    canonical ??= name;
                }
            }
            else if (IsThisClass(type))
            {
                var family = FamilyName(type);
                canonical = family ?? InferName(type);
                // The forward name→type slot (value-type resolution) is claimed
                // ONLY by an @this that IS a PLang value — one that inherits
                // app.type.item.@this. Engine mechanics that merely follow the @this
                // naming pattern but are not values (app.variable.path — a
                // value-graph navigation path, not a filesystem path — and
                // app.variable.list) would otherwise shadow the real value types
                // (path, list) in this slot by reflection order. The reverse
                // type→name (concept name, reported as .Kind for non-value concepts
                // like app/callstack/trace) still records below for every @this.
                // A variant resolves TO its family name but never claims the name
                // slot — the family base owns name→type (FilePath answers "path"
                // for ResolveName; ResolveType("path") stays path.@this).
                if (canonical != null && family == null
                    && typeof(app.type.item.@this).IsAssignableFrom(type))
                    _nameToType.TryAdd(canonical, type);
            }

            if (canonical != null)
            {
                _typeToName.TryAdd(type, canonical);
                // The raw CLR shapes this value type owns (int→"number") — the born-native lift's clr rung.
                if (type.GetProperty("OwnedClrTypes", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                        ?.GetValue(null) is IEnumerable<global::app.type.convert.OwnedClr> owned)
                    foreach (var decl in owned)
                        if (!decl.Assignable) _clr.TryAdd(decl.Clr, canonical);
            }
        }
    }

    private static bool IsThisClass(Type type) =>
        string.Equals(type.Name, "this", StringComparison.Ordinal);

    /// <summary>
    /// A scheme/variant class — an <c>@this</c> deriving from another family's
    /// <c>@this</c> (FilePath : path.@this) — IS that family on the PLang
    /// surface; its leaf namespace names the scheme, not a type. <c>item</c> is
    /// the apex every value type derives from, so it never counts as a family
    /// here. Returns null for a direct family (text, dict, file, …).
    /// </summary>
    private static string? FamilyName(Type type)
    {
        for (var b = type.BaseType; b != null && b != typeof(object); b = b.BaseType)
        {
            if (!IsThisClass(b)) continue;
            if (b == typeof(app.type.item.@this)) return null;
            return FamilyName(b) ?? InferName(b);
        }
        return null;
    }

    /// <summary>
    /// Inferred name: last namespace segment for @this classes, lowercased class
    /// name otherwise. Null when the type has no namespace.
    /// </summary>
    private static string? InferName(Type type)
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

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}
