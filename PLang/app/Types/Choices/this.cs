using System.Reflection;

namespace app.Types.Choices;

/// <summary>
/// Registry for [Choices]-attributed vocabulary providers — the closed string lists
/// the LLM may emit for slots whose parameter type has one. Build-time validator and
/// catalog Describe() both go through here; resolution (what each type does with the
/// chosen string at runtime) is the type's own concern and lives elsewhere.
///
/// The registry scans the PLang assembly once on first access and caches a
/// Type → MethodInfo map. Look-up unwraps Nullable&lt;T&gt; and Data&lt;T&gt; before
/// matching so callers can pass either the parameter declared type or the underlying
/// constrained type. Reachable as <c>app.Types.Choices</c>.
/// </summary>
public sealed class @this
{
    private readonly object _gate = new();
    private Dictionary<System.Type, MethodInfo>? _registry;

    /// <summary>
    /// Returns the choices for <paramref name="type"/> if it (or its underlying
    /// constrained type — through Nullable and Data wrappers) has a [Choices] method.
    /// Returns null when no provider is registered.
    /// </summary>
    public string[]? Get(System.Type type, Actor.Context.@this? context = null)
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
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Data.@this<>))
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
                    if (ps.Length != 1 || ps[0].ParameterType != typeof(Actor.Context.@this))
                        throw new System.InvalidOperationException(
                            $"[Choices] method {t.FullName}.{m.Name} must take a single Actor.Context.@this? parameter (nullable — static vocabularies ignore it).");
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
