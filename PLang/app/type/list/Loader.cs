using System.Reflection;

namespace app.type.list;

/// <summary>
/// Runtime scan-and-register for type-bearing assemblies — the type-system
/// analogue of <c>code.load</c>'s ICode discovery. Pulled out as a static
/// helper so unit tests can pass a known assembly directly without going
/// through the full DLL-load path.
///
/// <para>For every <see cref="app.Attributes.PlangTypeAttribute"/>-bearing
/// class in <paramref name="assembly"/>, calls
/// <see cref="@this.Register"/> (which routes through the registry's
/// runtime layer — runtime wins over generator-emitted entries at
/// <see cref="@this.ResolveType"/>).</para>
///
/// <para>For every <see cref="ITypeRenderer"/> implementation with a
/// parameterless constructor, instantiates it and calls
/// <see cref="renderer.@this.Register"/> (runtime wins over generator-emitted
/// renderers at <see cref="renderer.@this.Of"/>).</para>
///
/// <para><strong>Honest limit:</strong> registrations change <em>resolution
/// and rendering</em> going forward — name → CLR type, and how a value
/// serializes. They do NOT rewrite what the source generator already baked
/// at PLang build: PLNG-validated parameter slots, the <c>Data&lt;int&gt;</c>
/// slots on already-compiled action handlers, the type stamps in shipped
/// <c>.pr</c> files. Adding new types is unconstrained; overwriting
/// built-ins is "new resolution + new rendering, same compiled slots."</para>
/// </summary>
public static class Loader
{
    public sealed record Result(
        bool Success,
        string? ErrorKey,
        string? ErrorMessage,
        IReadOnlyList<string> RegisteredTypes,
        IReadOnlyList<(string TypeName, string Format)> RegisteredRenderers);

    /// <summary>
    /// Built-in type names a runtime-loaded DLL may not shadow. The bodies
    /// of these types are signing- or transport-load-bearing — a DLL that
    /// replaced <c>identity</c>'s CLR type or its renderer could produce
    /// authentically-signed envelopes whose body was attacker-composed.
    /// Primitives (<c>int</c>, <c>string</c>, <c>path</c>) stay overridable
    /// because their body is constrained by the type itself.
    ///
    /// <para><strong>When to extend:</strong> any time a new
    /// <c>[PlangType]</c>-decorated class joins the catalog whose body is
    /// signing-load-bearing (its bytes get signed by an actor's key) or
    /// transport-load-bearing (its bytes are persisted / round-tripped
    /// across the wire and trusted on read), add its PLang name here.
    /// Forgetting to extend the list means a runtime-loaded DLL can
    /// silently substitute the type's wire body.</para>
    /// </summary>
    public static readonly System.Collections.Generic.IReadOnlySet<string> SealedNames =
        new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "identity", "signature", "signedoperation", "callback", "channel",
        };

    /// <summary>
    /// The reserved core of the navigation planes — `%x!type%`/`!error%`/
    /// `!success%` and the `@schema` wire marker always answer from the Data
    /// envelope. A value type may not declare an instance property under these
    /// names: it would silently shadow (or be shadowed by) the envelope on the
    /// `!` plane, and which one wins becomes resolution-order trivia.
    /// </summary>
    public static readonly System.Collections.Generic.IReadOnlySet<string> ReservedCore =
        new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "type", "error", "success", "@schema",
        };

    /// <summary>
    /// The reserved-core instance property a candidate type illegally declares,
    /// or null when clean. Statics are fine (the lattice convention is a static
    /// named <c>Type</c>); only the navigable instance surface can shadow.
    /// </summary>
    public static string? ReservedShadow(System.Type clr)
    {
        foreach (var p in clr.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (ReservedCore.Contains(p.Name))
                return p.Name;
        return null;
    }

    public static Result Register(Assembly assembly, @this registry)
    {
        if (assembly == null) throw new System.ArgumentNullException(nameof(assembly));
        if (registry == null) throw new System.ArgumentNullException(nameof(registry));

        var types = new List<string>();
        var renderersList = new List<(string, string)>();

        System.Type[] exported;
        try { exported = assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            exported = ex.Types.Where(t => t != null).ToArray()!;
        }

        // First pass: PlangType registrations.
        foreach (var clr in exported)
        {
            if (clr == null || clr.IsAbstract || clr.IsInterface) continue;
            var attrs = clr.GetCustomAttributes<app.Attributes.PlangTypeAttribute>(inherit: false).ToList();
            // Reserved-core shadow check applies to anything registrable
            // ([PlangType] or @this-convention) before any registration lands.
            if ((attrs.Count > 0 || string.Equals(clr.Name, "this", System.StringComparison.Ordinal))
                && ReservedShadow(clr) is { } shadowed)
                return new Result(false, "TypeLoadReservedShadow",
                    $"Type '{clr.FullName}' declares instance property '{shadowed}' — `type`/`error`/`success`/`@schema` are the reserved navigation core and may not be shadowed by a value type.",
                    types, renderersList);
            string? canonical = null;
            if (attrs.Count > 0)
            {
                foreach (var a in attrs)
                {
                    var name = a.Name ?? InferName(clr);
                    if (name == null) continue;
                    if (SealedNames.Contains(name))
                        return new Result(false, "TypeLoadCollision",
                            $"[PlangType('{name}')] is reserved — '{name}' is on the sealed built-in list and may not be shadowed by a runtime-loaded DLL.",
                            types, renderersList);
                    registry.Register(name, clr);
                    canonical ??= name;
                }
            }
            else if (string.Equals(clr.Name, "this", System.StringComparison.Ordinal))
            {
                canonical = InferName(clr);
                if (canonical != null)
                {
                    if (SealedNames.Contains(canonical))
                        return new Result(false, "TypeLoadCollision",
                            $"Inferred PlangType name '{canonical}' is on the sealed built-in list and may not be shadowed by a runtime-loaded DLL.",
                            types, renderersList);
                    registry.Register(canonical, clr);
                }
            }
            if (canonical != null) types.Add(canonical);
        }

        // Second pass: ITypeRenderer instantiation + registration.
        foreach (var clr in exported)
        {
            if (clr == null || clr.IsAbstract || clr.IsInterface) continue;
            if (!typeof(ITypeRenderer).IsAssignableFrom(clr)) continue;
            var ctor = clr.GetConstructor(System.Type.EmptyTypes);
            if (ctor == null) continue; // parameterless-ctor rule mirrors code.load
            ITypeRenderer instance;
            try { instance = (ITypeRenderer)ctor.Invoke(null); }
            catch (System.Exception ex) when (ex is not (System.OutOfMemoryException or System.StackOverflowException))
            { continue; }
            if (SealedNames.Contains(instance.TypeName))
                return new Result(false, "TypeLoadCollision",
                    $"ITypeRenderer for '{instance.TypeName}' rejected — '{instance.TypeName}' is on the sealed built-in list and its rendering may not be replaced by a runtime-loaded DLL.",
                    types, renderersList);
            registry.Renderer.Register(instance.TypeName, instance.Format,
                (value, writer) => instance.Write(value, writer));
            renderersList.Add((instance.TypeName, instance.Format));
        }

        // Coverage check: every newly-registered [PlangType] must have at least
        // one renderer (either freshly registered or already in the dispatch
        // table from earlier loads / built-ins).
        foreach (var typeName in types)
        {
            if (!registry.Renderer.Has(typeName))
            {
                return new Result(false, "TypeLoadCoverage",
                    $"[PlangType] '{typeName}' loaded with no covering renderer (need a Default ITypeRenderer or per-format coverage).",
                    types, renderersList);
            }
        }

        return new Result(true, null, null, types, renderersList);
    }

    private static string? InferName(System.Type type)
    {
        if (string.Equals(type.Name, "this", System.StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(type.Namespace)) return null;
            var ns = type.Namespace!;
            var lastDot = ns.LastIndexOf('.');
            return (lastDot >= 0 ? ns[(lastDot + 1)..] : ns).ToLowerInvariant();
        }
        return type.Name.ToLowerInvariant();
    }
}
