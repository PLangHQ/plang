using System.Collections.Concurrent;
using System.Reflection;

namespace app.types.renderers;

/// <summary>
/// Per-(type, format) renderer dispatch — the table the writers consult
/// when they see a <see cref="app.data.TypedValueNode"/> in their <c>Value</c>
/// stream. One entry per (typeName, formatToken) pair; the wildcard token
/// <c>"*"</c> covers any format the type renders uniformly.
///
/// <para>Discovery scans <c>app/types/&lt;name&gt;/serializer/&lt;format&gt;.cs</c>
/// — each file is a <c>public static class</c> exposing a static
/// <c>Write(TConcrete value, IWriter writer)</c>. The file name maps to the
/// format token (<c>Default.cs</c> → wildcard <c>"*"</c>); the parent folder
/// name maps to the PLang type name. Discovery is lazy + cached.</para>
///
/// <para>The runtime-registration seam (<see cref="Register"/>) is the
/// hook DLLs loaded via <c>code.load</c> use to drop in renderers for
/// runtime-registered types. Runtime registrations shadow generator-emitted
/// entries — same precedence as <see cref="app.types.@this.ResolveType"/>.</para>
/// </summary>
public sealed class @this
{
    /// <summary>Wildcard format token — covers any format the type renders uniformly.</summary>
    public const string AnyFormat = "*";

    public delegate void Write(object value, app.channel.serializer.IWriter writer);

    private readonly ConcurrentDictionary<(string Type, string Format), Write> _generated = new();
    private readonly ConcurrentDictionary<(string Type, string Format), Write> _runtime = new();
    private readonly ConcurrentDictionary<string, bool> _hasAny = new();

    private readonly object _initLock = new();
    private bool _initialized;

    /// <summary>
    /// Assemblies to scan for <c>serializer/&lt;format&gt;.cs</c> classes.
    /// Defaults to the App assembly; consumers can extend (test fixtures).
    /// </summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    /// <summary>
    /// Returns the <see cref="Write"/> delegate registered for
    /// <paramref name="typeName"/> + <paramref name="format"/>, falling back
    /// to the wildcard <c>"*"</c> entry, or null when neither is present.
    /// Runtime registrations win over generated entries.
    /// </summary>
    public Write? Of(string typeName, string format)
    {
        EnsureInitialized();
        if (_runtime.TryGetValue((typeName, format), out var rt)) return rt;
        if (_generated.TryGetValue((typeName, format), out var gen)) return gen;
        if (_runtime.TryGetValue((typeName, AnyFormat), out var rtAny)) return rtAny;
        if (_generated.TryGetValue((typeName, AnyFormat), out var genAny)) return genAny;
        return null;
    }

    /// <summary>True when at least one renderer (any format) is registered for the type.</summary>
    public bool Has(string typeName)
    {
        EnsureInitialized();
        return _hasAny.ContainsKey(typeName);
    }

    /// <summary>
    /// Runtime registration — for DLLs loaded via <c>code.load</c> that ship
    /// renderers for runtime-registered types. Stage 7 wires this end to end.
    /// Already-registered (type, format) entries are replaced (runtime wins).
    /// </summary>
    public void Register(string typeName, string format, Write write)
    {
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(format) || write == null) return;
        _runtime[(typeName, format)] = write;
        _hasAny[typeName] = true;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            foreach (var asm in Assemblies)
                IndexAssembly(asm);
            _initialized = true;
        }
    }

    private void IndexAssembly(Assembly assembly)
    {
        foreach (var type in SafeGetTypes(assembly))
        {
            if (!type.IsClass || !type.IsAbstract || !type.IsSealed) continue; // static class
            if (string.IsNullOrEmpty(type.Namespace)) continue;
            if (!type.Namespace!.EndsWith(".serializer", System.StringComparison.Ordinal)) continue;

            // namespace shape: app.types.<typeName>.serializer
            var ns = type.Namespace;
            var pivot = ns.LastIndexOf(".serializer", System.StringComparison.Ordinal);
            var head = ns[..pivot];
            var lastDot = head.LastIndexOf('.');
            if (lastDot < 0) continue;
            var typeName = head[(lastDot + 1)..];

            // file/class name maps to format token; "Default" → wildcard.
            var format = type.Name.Equals("Default", System.StringComparison.Ordinal)
                ? AnyFormat
                : type.Name.ToLowerInvariant();

            var method = type.GetMethod("Write",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (method == null) continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 2) continue;
            if (parameters[1].ParameterType != typeof(app.channel.serializer.IWriter)) continue;

            Write del = (value, writer) => method.Invoke(null, new object?[] { value, writer });

            _generated[(typeName, format)] = del;
            _hasAny[typeName] = true;
        }
    }

    private static IEnumerable<System.Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}
