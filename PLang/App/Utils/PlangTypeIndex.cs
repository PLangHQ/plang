using System.Collections.Concurrent;
using System.Reflection;
using App.Attributes;

namespace App.Utils;

/// <summary>
/// Resolves type names for domain types declared via [PlangType] or the @this
/// naming convention. Acts as the single source of truth for type identity —
/// TypeMapping delegates to this class instead of holding a hand-maintained
/// dictionary of domain types.
///
/// Rules, in order:
///   1. [PlangType("name")] on the class — the declared name wins. Multiple
///      [PlangType] attributes act as aliases; the first attribute's Name is
///      canonical, the rest resolve on lookup.
///   2. [PlangType] with no Name — inferred name (@this convention: last
///      namespace segment; otherwise class name lowercased).
///   3. For @this classes WITHOUT [PlangType], the last-namespace-segment is
///      still the canonical name — the OBP convention makes these catalog-visible
///      without needing to tag each one.
///   4. Otherwise: the type has no PLang name and is opaque.
/// </summary>
public static class PlangTypeIndex
{
    private static readonly object _initLock = new();
    private static bool _initialized;
    private static readonly ConcurrentDictionary<string, Type> _nameToType = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<Type, string> _typeToName = new();
    // Runtime registrations (test harnesses, plugins) merge into the static index.
    private static readonly ConcurrentDictionary<string, Type> _runtimeNameToType = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Assemblies to scan. Defaults to the App assembly; callers can extend.</summary>
    public static List<Assembly> Assemblies { get; } = new() { typeof(PlangTypeIndex).Assembly };

    private static readonly HashSet<string> _clrTypeFullNames = new(StringComparer.Ordinal);
    private static bool _clrTypeFullNamesInitialized;
    private static readonly object _clrTypeFullNamesLock = new();

    /// <summary>
    /// True if <paramref name="name"/> matches the FullName of any type in any loaded assembly.
    /// Used to defend goal-name slots against CLR-type-name leaks (a known builder bug:
    /// e.g. `App.Goals.Goal.GoalCall` getting written as a goal Name during prompt rendering).
    /// A goal Name is a user-authored identifier — it can never legitimately equal a CLR type name.
    /// </summary>
    public static bool IsClrTypeName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        // Cheap pre-filter: must contain a dot and look like a namespaced type.
        if (!name.Contains('.')) return false;
        EnsureClrTypeFullNamesInitialized();
        return _clrTypeFullNames.Contains(name);
    }

    private static void EnsureClrTypeFullNamesInitialized()
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
    /// Returns the canonical PLang name for a domain type, or null if the type
    /// is not named (no [PlangType], not an @this class, not registered at runtime).
    /// </summary>
    public static string? ResolveName(Type type)
    {
        EnsureInitialized();
        return _typeToName.TryGetValue(type, out var name) ? name : null;
    }

    /// <summary>
    /// Returns the CLR type for a PLang name, or null if no type is registered
    /// under that name.
    /// </summary>
    public static Type? ResolveType(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        EnsureInitialized();
        if (_runtimeNameToType.TryGetValue(name, out var runtime)) return runtime;
        return _nameToType.TryGetValue(name, out var t) ? t : null;
    }

    /// <summary>
    /// All types known to the index, deduplicated by CLR type. Useful for seeding
    /// a catalog build that isn't started from action parameters (e.g. a schema
    /// dump of every declared domain type).
    /// </summary>
    public static IEnumerable<Type> KnownTypes()
    {
        EnsureInitialized();
        return _typeToName.Keys.Concat(_runtimeNameToType.Values).Distinct();
    }

    /// <summary>
    /// Registers a name → type mapping at runtime. Prefer [PlangType] on the class.
    /// This is for synthetic/test types that can't carry attributes.
    /// </summary>
    public static void RegisterRuntime(string name, Type type)
    {
        if (string.IsNullOrWhiteSpace(name) || type == null) return;
        _runtimeNameToType[name] = type;
        _typeToName.TryAdd(type, name);
    }

    /// <summary>Forces the assembly scan to re-run on next access. Tests use this.</summary>
    public static void Reset()
    {
        lock (_initLock)
        {
            _initialized = false;
            _nameToType.Clear();
            _typeToName.Clear();
        }
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            foreach (var assembly in Assemblies)
                IndexAssembly(assembly);
            _initialized = true;
        }
    }

    private static void IndexAssembly(Assembly assembly)
    {
        foreach (var type in SafeGetTypes(assembly))
        {
            if (type.IsAbstract && !type.IsSealed) continue; // skip abstract (non-static)

            var attrs = type.GetCustomAttributes<PlangTypeAttribute>(inherit: false).ToList();
            string? canonical = null;

            if (attrs.Count > 0)
            {
                // Every attribute name resolves to this type; first non-null Name is canonical.
                foreach (var attr in attrs)
                {
                    var name = attr.Name ?? InferName(type);
                    if (name == null) continue;
                    _nameToType.TryAdd(name, type);
                    canonical ??= name;
                }
            }
            else if (IsThisClass(type))
            {
                // @this convention — always catalog-visible by the OBP rule.
                canonical = InferName(type);
                if (canonical != null)
                    _nameToType.TryAdd(canonical, type);
            }

            if (canonical != null)
                _typeToName.TryAdd(type, canonical);
        }
    }

    private static bool IsThisClass(Type type) =>
        string.Equals(type.Name, "this", StringComparison.Ordinal);

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
