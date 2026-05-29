using System.Collections.Concurrent;
using System.Reflection;
using app.channel.serializer.filter;

namespace app.data;

/// <summary>
/// Data tree-walker reconstruction — the reverse direction of
/// <see cref="@this.Normalize"/>: given a value
/// that's been normalized into the uniform tree
/// (<c>primitive | byte[] | Data | List&lt;&gt;</c>), reconstruct an instance
/// of T by recursively walking the tree, dispatching per child Data by name
/// onto T's <c>[Out]</c> properties.</para>
///
/// <para>Per-type reconstruction hooks let types that can't be naively
/// populated from a property bag (path — abstract, no parameterless ctor,
/// needs Context to wire scheme registry) override the generic flow.
/// Discovery is convention-driven: a public static method named
/// <c>FromNormalized(Data, Context)</c> on T wins; for <c>path.@this</c>
/// the existing <c>Resolve(string, Context)</c> is wrapped as the hook.</para>
///
/// <para>Hook lookups and property tables cache per type. Reflection fires
/// once per (T, mode) per process.</para>
/// </summary>
public partial class @this
{
    private static readonly ConcurrentDictionary<System.Type, Func<@this, actor.context.@this?, object?>?> _hookCache = new();
    private static readonly ConcurrentDictionary<System.Type, PropertyInfo[]> _settablePropsCache = new();

    /// <summary>
    /// Reconstruct an instance of <typeparamref name="T"/> by walking the
    /// normalized tree carried in <see cref="Value"/>. Mirrors
    /// <see cref="Normalize"/>'s output shape.
    /// </summary>
    public T? Reconstruct<T>(actor.context.@this? context = null)
    {
        var ctx = context ?? _context;
        // Skip Normalize when Value is already a tree-native leaf — those
        // cases (null, primitives, byte[], BCL leaf structs, enums, nested
        // Data) re-walk to themselves in Normalize. Domain objects and raw
        // collections still need Normalize to decompose into the property-bag
        // shape Walk consumes.
        var raw = Value;
        var tree = IsLeafShape(raw) ? raw : Normalize();
        var result = Walk(tree, typeof(T), ctx);
        if (result is null) return default;
        if (result is T typed) return typed;
        // Final coercion through TypeMapping for primitive-to-T conversions
        // the walker didn't catch (e.g. int → long).
        return (T?)AppTypes.ConvertTo(result, typeof(T));
    }

    private static bool IsLeafShape(object? v)
        => v is null
        || v is string || v is bool
        || v is int || v is long || v is double || v is float || v is decimal
        || v is System.DateTime || v is System.DateTimeOffset
        || v is System.TimeSpan || v is System.Guid
        || v is byte[] || v is System.Enum
        || v is @this;

    private static object? Walk(object? value, System.Type targetType, actor.context.@this? ctx)
    {
        if (value is null) return null;

        // Unwrap nullable.
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Primitive + tree-leaf passthrough.
        if (IsLeafTarget(underlying))
        {
            if (underlying.IsInstanceOfType(value)) return value;
            return AppTypes.ConvertTo(value, underlying);
        }

        // Per-type hook wins over generic property-bag construction.
        var hook = GetHook(underlying);
        if (hook != null)
        {
            // The hook expects a Data envelope, not the bare tree.
            var envelope = value is @this d ? d : new @this("", value);
            return hook(envelope, ctx);
        }

        // List<X> — walk the tree's IList and convert per element.
        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(List<>))
        {
            var elementType = underlying.GetGenericArguments()[0];
            var listInstance = (System.Collections.IList)Activator.CreateInstance(underlying)!;
            if (value is System.Collections.IEnumerable seq)
            {
                foreach (var item in seq)
                {
                    var elementValue = item is @this child ? child.Value : item;
                    listInstance.Add(Walk(elementValue, elementType, ctx));
                }
            }
            return listInstance;
        }

        // Dictionary<K, V> — tree is List<Data>; each child's Name is the key.
        if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var keyType = underlying.GetGenericArguments()[0];
            var valueType = underlying.GetGenericArguments()[1];
            var dictInstance = (System.Collections.IDictionary)Activator.CreateInstance(underlying)!;
            if (value is System.Collections.IEnumerable seq)
            {
                foreach (var item in seq)
                {
                    if (item is not @this child) continue;
                    var k = AppTypes.ConvertTo(child.Name, keyType);
                    if (k == null)
                    {
                        throw new NormalizeException(
                            $"Reconstruct can't convert dictionary key '{child.Name}' to {keyType.Name}.",
                            "NormalizeReconstructFailed");
                    }
                    var v = Walk(child.Value, valueType, ctx);
                    dictInstance[k] = v;
                }
            }
            return dictInstance;
        }

        // Generic property-bag reconstruction.
        return ReconstructObject(value, underlying, ctx);
    }

    private static object? ReconstructObject(object? value, System.Type targetType, actor.context.@this? ctx)
    {
        if (value is null) return null;

        // Gather children by name first — needed for both parameterless and
        // positional-ctor paths.
        var byName = new Dictionary<string, @this>(StringComparer.OrdinalIgnoreCase);
        if (value is System.Collections.IEnumerable seqEarly)
            foreach (var item in seqEarly)
                if (item is @this child) byName[child.Name] = child;

        // Positional-ctor path: types with no parameterless ctor (records with
        // positional parameters, immutable classes) reconstruct by gathering
        // the matching children and calling the longest public ctor.
        if (targetType.GetConstructor(System.Type.EmptyTypes) == null)
        {
            var ctors = targetType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
                throw new NormalizeException(
                    $"No reconstruction strategy for {targetType.Name}: no public ctor and no FromNormalized hook found.",
                    "NormalizeNoReconstructionStrategy");
            // Prefer the ctor whose parameter names all match available children.
            var best = ctors.OrderByDescending(c => c.GetParameters().Length).First();
            var pars = best.GetParameters();
            var args = new object?[pars.Length];
            for (int i = 0; i < pars.Length; i++)
            {
                var pname = pars[i].Name ?? "";
                if (byName.TryGetValue(pname.ToLowerInvariant(), out var c))
                    args[i] = Walk(c.Value, pars[i].ParameterType, ctx);
                else if (pars[i].HasDefaultValue)
                    args[i] = pars[i].DefaultValue;
                else
                    args[i] = pars[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(pars[i].ParameterType)
                        : null;
            }
            try
            {
                return best.Invoke(args);
            }
            catch (System.Exception ex)
            {
                throw new NormalizeException(
                    $"Reconstruct failed invoking positional ctor of {targetType.Name}: {ex.Message}",
                    "NormalizeReconstructFailed", ex);
            }
        }

        object? instance;
        try
        {
            instance = Activator.CreateInstance(targetType);
        }
        catch (MissingMethodException)
        {
            throw new NormalizeException(
                $"No reconstruction strategy for {targetType.Name}: neither parameterless ctor nor FromNormalized hook found.",
                "NormalizeNoReconstructionStrategy");
        }
        catch (System.Exception ex)
        {
            // Parameterless ctor exists but its body threw — distinct from
            // strategy-missing. Surface the original failure.
            throw new NormalizeException(
                $"Reconstruct failed instantiating {targetType.Name}: {ex.Message}",
                "NormalizeReconstructFailed", ex);
        }

        if (byName.Count == 0) return instance;

        var settable = _settablePropsCache.GetOrAdd(targetType, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanWrite && p.GetSetMethod(nonPublic: false) != null)
             .ToArray());

        foreach (var prop in settable)
        {
            // Match by lowercased property name (Normalize lowercases on emission).
            if (!byName.TryGetValue(prop.Name.ToLowerInvariant(), out var child)) continue;
            try
            {
                var v = Walk(child.Value, prop.PropertyType, ctx);
                prop.SetValue(instance, v);
            }
            catch (NormalizeException) { throw; }
            catch (System.Exception ex)
            {
                throw new NormalizeException(
                    $"Reconstruct failed setting {targetType.Name}.{prop.Name}: {ex.Message}",
                    "NormalizeReconstructFailed", ex);
            }
        }

        return instance;
    }

    private static bool IsLeafTarget(System.Type t)
        => t.IsPrimitive
        || t == typeof(string) || t == typeof(decimal)
        || t == typeof(System.DateTime) || t == typeof(System.DateTimeOffset)
        || t == typeof(System.TimeSpan) || t == typeof(System.Guid)
        || t == typeof(byte[])
        || t.IsEnum;

    private static Func<@this, actor.context.@this?, object?>? GetHook(System.Type targetType)
        => _hookCache.GetOrAdd(targetType, DiscoverHook);

    private static Func<@this, actor.context.@this?, object?>? DiscoverHook(System.Type targetType)
    {
        // Convention 1 — explicit FromNormalized(Data, Context) on the target type.
        var fromNormalized = targetType.GetMethod(
            "FromNormalized",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(@this), typeof(actor.context.@this) },
            null);
        if (fromNormalized != null)
            return (data, ctx) => fromNormalized.Invoke(null, new object?[] { data, ctx });

        // Convention 2 — path.@this and subclasses: read the "relative" child
        // from the normalized tree and call Resolve(relative, ctx).
        // Note: the "scheme" child on the wire is informational only — path
        // strings carry their scheme prefix ("http://…", absolute file paths)
        // so Resolve recovers the scheme without consulting the field. The
        // wire field exists for receivers that want to dispatch without
        // parsing; this hook doesn't.
        if (typeof(app.types.path.@this).IsAssignableFrom(targetType))
        {
            return (data, ctx) =>
            {
                if (ctx == null)
                    throw new NormalizeException(
                        $"Reconstructing {targetType.Name} requires a Context — pass one to As<>/Reconstruct<>.",
                        "NormalizeContextRequired");

                string? relative = null;
                if (data.Value is System.Collections.IEnumerable children)
                {
                    foreach (var item in children)
                    {
                        if (item is @this child && child.Name.Equals("relative", StringComparison.OrdinalIgnoreCase))
                        {
                            relative = child.Value?.ToString();
                            break;
                        }
                    }
                }
                else if (data.Value is string raw)
                {
                    // Bridge: incoming as a bare string (pre-Stage-2-wiring shape).
                    relative = raw;
                }

                if (relative == null)
                    throw new NormalizeException(
                        $"Reconstructing {targetType.Name}: normalized tree has no 'relative' child.",
                        "NormalizeMissingRelative");

                return app.types.path.@this.Resolve(relative, ctx);
            };
        }

        return null;
    }

    /// <summary>Test-only — clears the hook + settable-prop caches.</summary>
    internal static void ClearReconstructCachesForTests()
    {
        _hookCache.Clear();
        _settablePropsCache.Clear();
    }

    /// <summary>Test-only — current hook cache size.</summary>
    internal static int HookCacheSize => _hookCache.Count;
}
