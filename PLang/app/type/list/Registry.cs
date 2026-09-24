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
///   3. @this classes WITHOUT [PlangType] — last-namespace-segment is the name.
///   4. Only items (app.type.item.@this) are plang types; an engine class has no type name.
///   One name, one class, in both directions — checked when the registry is built.
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
        // One name, one class — a runtime registration (code.load, a plugin) cannot take a name
        // another class already owns.
        EnsureInitialized();
        if (ResolveType(name) is { } owner && owner != type)
            throw new InvalidOperationException(
                $"type name '{name}' is claimed by both {owner.FullName} and {type.FullName} — one name, one class.");
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
            // An explicitly-aliased [PlangType] name (one that diverges from the type's own
            // inferred name) claims its name after every natural owner has registered.
            var deferredAliases = new List<(string Name, Type Type)>();
            foreach (var assembly in Assemblies)
                IndexAssembly(assembly, deferredAliases);
            foreach (var (name, type) in deferredAliases)
                Claim(name, type);
            SeedAliases();
            Guard();
            _initialized = true;
        }
    }

    /// <summary>
    /// The spelled names of the primitives (<c>string</c>, <c>int</c>, <c>boolean</c>, <c>csv</c>, …)
    /// resolve to the ITEM that owns the alias's C# type — <c>string</c> → text, <c>int</c> → number.
    /// The C# types are the items' mates (their <c>OwnedClrTypes</c>), never a name's owner.
    /// </summary>
    private void SeedAliases()
    {
        foreach (var (alias, clr) in app.type.primitive.@this.Aliases)
        {
            var shape = Nullable.GetUnderlyingType(clr) ?? clr;
            var owner = typeof(app.type.item.@this).IsAssignableFrom(shape) ? shape
                : _clr.TryGetValue(shape, out var ownerName) && _nameToType.TryGetValue(ownerName, out var ot) ? ot
                : null;
            if (owner != null && !_nameToType.ContainsKey(alias)) _nameToType[alias] = owner;
        }
    }

    // One name, one class: a second class claiming a taken name fails at registry build.
    private void Claim(string name, Type type)
    {
        if (_nameToType.TryGetValue(name, out var existing) && existing != type)
            throw new InvalidOperationException(
                $"type name '{name}' is claimed by both {existing.FullName} and {type.FullName} — one name, one class.");
        _nameToType[name] = type;
    }

    // A class may report a name only if it is the item that owns the name, or a kind of it (a scheme
    // or program list deriving from the owner). The C# mates ride the ownership index, not this one.
    private void Guard()
    {
        foreach (var (type, name) in _typeToName)
        {
            if (!_runtimeNameToType.TryGetValue(name, out var owner) && !_nameToType.TryGetValue(name, out owner))
                throw new InvalidOperationException($"{type.FullName} reports the type name '{name}', which no item owns.");
            if (owner != type && !owner.IsAssignableFrom(type))
                throw new InvalidOperationException(
                    $"{type.FullName} reports the type name '{name}', which {owner.FullName} owns — one name, one class.");
        }
    }


    private void IndexAssembly(Assembly assembly, List<(string Name, Type Type)> deferredAliases)
    {
        foreach (var type in SafeGetTypes(assembly))
        {
            // The registry holds plang types — items — only. An engine class (a host list, a
            // channel, a reader) claims no type name; a non-item's [PlangType] declares a name its
            // OWNER reads (a closed set's name is the KIND of choice), never a type.
            if (!typeof(app.type.item.@this).IsAssignableFrom(type)) continue;
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
                    canonical ??= name;
                    // A name that matches the type's own inference is its natural
                    // claim — register now. A name the attribute redirects to
                    // (one that diverges from the type's namespace/class) is claimed
                    // after every natural owner has registered.
                    if (string.Equals(name, inferred, System.StringComparison.Ordinal))
                        Claim(name, type);
                    else
                        deferredAliases.Add((name, type));
                }
            }
            else if (IsThisClass(type))
            {
                var family = FamilyName(type);
                canonical = family ?? InferName(type);
                // A variant resolves TO its family name but never claims the name
                // slot — the family base owns name→type (FilePath answers "path";
                // ResolveType("path") stays path.@this).
                if (canonical != null && family == null)
                    Claim(canonical, type);
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
    /// The family a class is a KIND of — only when it says so. A path scheme declares it
    /// (<c>[PathScheme("file")]</c> → {path, kind: file}); a typed program list is one through its
    /// generic base (<c>list&lt;step&gt;</c> → {list, kind: step}). Null for every other class:
    /// by default a class's name is its own (modifier is "modifier", never a kind of action).
    /// </summary>
    private static string? FamilyName(Type type)
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
