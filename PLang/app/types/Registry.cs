using System.Collections.Concurrent;
using System.Reflection;
using app.Attributes;

namespace app.types;

/// <summary>
/// Registry partial of <see cref="@this"/> — absorbs the former <c>PlangTypeIndex</c>
/// (single source of truth for domain type identity).
///
/// Rules, in order:
///   1. [PlangType("name")] on the class — declared name wins. Multiple
///      [PlangType] attributes act as aliases; the first non-null Name is canonical.
///   2. [PlangType] with no Name — inferred name (@this convention: last
///      namespace segment; otherwise class name lowercased).
///   3. @this classes WITHOUT [PlangType] — last-namespace-segment is still
///      canonical (the OBP convention makes them catalog-visible by default).
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

    private readonly HashSet<string> _clrTypeFullNames = new(StringComparer.Ordinal);
    private volatile bool _clrTypeFullNamesInitialized;
    private readonly object _clrTypeFullNamesLock = new();

    /// <summary>Assemblies to scan for [PlangType] discovery. Defaults to the App assembly; callers can extend.</summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    /// <summary>
    /// True if <paramref name="name"/> matches the FullName of any type in any loaded assembly.
    /// Used to defend goal-name slots against CLR-type-name leaks (a known builder bug:
    /// e.g. <c>app.goals.goal.GoalCall</c> getting written as a goal Name during prompt rendering).
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
    /// Returns the canonical PLang name for a domain type, or null if the type
    /// is not named (no [PlangType], not an @this class, not registered at runtime).
    /// </summary>
    public string? ResolveName(Type type)
    {
        EnsureInitialized();
        return _typeToName.TryGetValue(type, out var name) ? name : null;
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
    }

    private void EnsureInitialized()
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

    private void IndexAssembly(Assembly assembly)
    {
        foreach (var type in SafeGetTypes(assembly))
        {
            var attrs = type.GetCustomAttributes<PlangTypeAttribute>(inherit: false).ToList();

            // Skip abstract (non-static) types UNLESS they declare [PlangType] — an
            // abstract [PlangType] is the base of a scheme/family (e.g. path.@this with
            // concrete subclasses FilePath, HttpPath). The PLang name resolves to the
            // base; construction dispatches via a registry (Scheme.From).
            if (type.IsAbstract && !type.IsSealed && attrs.Count == 0) continue;
            string? canonical = null;

            if (attrs.Count > 0)
            {
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
