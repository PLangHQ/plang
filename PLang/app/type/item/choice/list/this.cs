using System.Reflection;

namespace app.type.item.choice.list;

/// <summary>
/// Registry for [Choices]-attributed vocabulary providers — the closed string lists
/// the LLM may emit for slots whose parameter type has one. Build-time validator and
/// catalog Describe() both go through here; resolution (what each type does with the
/// chosen string at runtime) is the type's own concern and lives elsewhere.
///
/// The registry scans the PLang assembly once on first access and caches a
/// Type → MethodInfo map. Look-up unwraps Nullable&lt;T&gt; and Data&lt;T&gt; before
/// matching so callers can pass either the parameter declared type or the underlying
/// constrained type. Reachable as <c>app.type.choices</c>.
/// </summary>
public sealed class @this
{
    private readonly object _gate = new();
    private Dictionary<System.Type, MethodInfo>? _registry;

    // The owning type registry — a closed set registers its name (kind → choice<T>) and its
    // reader through here, so both facets stay under the concept that discovers them.
    private readonly global::app.type.list.@this _owner;
    internal @this(global::app.type.list.@this owner) { _owner = owner; }

    /// <summary>
    /// Register every closed set (<c>choice&lt;T&gt;</c>) reachable in <paramref name="assembly"/>
    /// so its NAME resolves (kind → <c>choice&lt;T&gt;</c>, the reverse of the forward catalog) and
    /// its values READ (a <c>Reader&lt;T&gt;</c> per set). A set is only identifiable by its usage —
    /// an enum choice carries no <c>[Choices]</c> marker — so this reflects the assembly's property
    /// types. Fired when an assembly is discovered: boot (the PLang assembly) and <c>code.load</c>
    /// (an external one), so a late-loaded module's choice params register the same as built-ins.
    /// Idempotent — re-registering the same set overwrites.
    /// </summary>
    public void Register(System.Reflection.Assembly assembly)
    {
        // The choice FAMILY — a wire type {name:"choice", kind:"operator"} resolves to choice<T>.
        _owner.Register("choice", typeof(global::app.type.item.choice.@this<>));

        var seen = new HashSet<System.Type>();
        foreach (var t in SafeTypes(assembly))
            foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var u = Unwrap(prop.PropertyType);
                if (!u.IsGenericType || u.GetGenericTypeDefinition() != typeof(global::app.type.item.choice.@this<>))
                    continue;
                if (!seen.Add(u)) continue;

                var inner = u.GetGenericArguments()[0];
                var kindName = _owner.GetTypeName(inner);
                // name → the choice<T> WRAPPER (the symbol→member Parse hook lives on choice<T>,
                // not the inner T — registering the inner leaves a value falling to generic JSON).
                _owner.Register(kindName, u);
                // the closed reader for this set — one reflective instantiation, then typed reads.
                _owner.Reader.Register("choice", kindName,
                    (global::app.type.reader.ITypeReader)System.Activator.CreateInstance(
                        typeof(global::app.type.item.choice.serializer.Reader<>).MakeGenericType(inner), kindName)!);
            }
    }

    // A code.load'd assembly may reference types it can't fully load; keep the ones that resolve.
    private static IEnumerable<System.Type> SafeTypes(System.Reflection.Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (System.Reflection.ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    /// <summary>
    /// Returns the choices for <paramref name="type"/> if it (or its underlying
    /// constrained type — through Nullable and Data wrappers) has a [Choices] method.
    /// Returns null when no provider is registered.
    /// </summary>
    public string[]? Get(System.Type type, actor.context.@this? context = null)
    {
        var underlying = Unwrap(type);
        var registry = EnsureRegistry();
        if (!registry.TryGetValue(underlying, out var method)) return null;
        return (string[])method.Invoke(null, [context])!;
    }

    /// <summary>
    /// True when <paramref name="type"/> (or its underlying constrained type) has a
    /// registered [Choices] provider. Used by the validator to decide between
    /// membership-check and the generic conversion path.
    /// </summary>
    public bool Has(System.Type type) =>
        EnsureRegistry().ContainsKey(Unwrap(type));

    private static System.Type Unwrap(System.Type type)
    {
        var n = Nullable.GetUnderlyingType(type);
        if (n != null) type = n;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(data.@this<>))
            type = type.GetGenericArguments()[0];
        return type;
    }

    private Dictionary<System.Type, MethodInfo> EnsureRegistry()
    {
        if (_registry != null) return _registry;
        lock (_gate)
        {
            if (_registry != null) return _registry;
            var map = new Dictionary<System.Type, MethodInfo>();
            var assembly = typeof(@this).Assembly;
            foreach (var t in assembly.GetExportedTypes())
            {
                MethodInfo? found = null;
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.GetCustomAttribute<app.Attributes.ChoicesAttribute>() == null) continue;
                    if (m.ReturnType != typeof(string[]))
                        throw new System.InvalidOperationException(
                            $"[Choices] method {t.FullName}.{m.Name} must return string[].");
                    var ps = m.GetParameters();
                    if (ps.Length != 1 || ps[0].ParameterType != typeof(actor.context.@this))
                        throw new System.InvalidOperationException(
                            $"[Choices] method {t.FullName}.{m.Name} must take a single actor.context.@this? parameter (nullable — static vocabularies ignore it).");
                    if (found != null)
                        throw new System.InvalidOperationException(
                            $"Type {t.FullName} declares more than one [Choices] method.");
                    found = m;
                }
                if (found != null) map[t] = found;
            }
            _registry = map;
            return _registry;
        }
    }
}
