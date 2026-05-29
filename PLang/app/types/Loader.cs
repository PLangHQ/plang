using System.Reflection;

namespace app.types;

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
/// <see cref="renderers.@this.Register"/> (runtime wins over generator-emitted
/// renderers at <see cref="renderers.@this.Of"/>).</para>
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
            string? canonical = null;
            if (attrs.Count > 0)
            {
                foreach (var a in attrs)
                {
                    var name = a.Name ?? InferName(clr);
                    if (name == null) continue;
                    registry.Register(name, clr);
                    canonical ??= name;
                }
            }
            else if (string.Equals(clr.Name, "this", System.StringComparison.Ordinal))
            {
                canonical = InferName(clr);
                if (canonical != null) registry.Register(canonical, clr);
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
            registry.Renderers.Register(instance.TypeName, instance.Format,
                (value, writer) => instance.Write(value, writer));
            renderersList.Add((instance.TypeName, instance.Format));
        }

        // Coverage check: every newly-registered [PlangType] must have at least
        // one renderer (either freshly registered or already in the dispatch
        // table from earlier loads / built-ins).
        foreach (var typeName in types)
        {
            if (!registry.Renderers.Has(typeName))
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
