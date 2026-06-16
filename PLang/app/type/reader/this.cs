using System.Collections.Concurrent;
using System.Reflection;

namespace app.type.reader;

/// <summary>
/// Per-(type, kind) reader dispatch — the read-side mirror of
/// <see cref="app.type.renderer.@this"/>. Where the renderer encodes a value
/// <em>into</em> a channel (keyed by the channel format), the reader decodes
/// the value's <em>own</em> raw source form (keyed by <c>kind</c> — the
/// encoding within the type's shape: <c>json</c>/<c>xml</c>/<c>yaml</c> for
/// <c>object</c>, <c>csv</c>/<c>xlsx</c> for <c>table</c>, <c>int</c>/<c>uint</c>
/// for <c>number</c>, <c>png</c>/<c>jpg</c> for <c>image</c>). The wildcard
/// token <c>"*"</c> covers any kind the type reads uniformly.
///
/// <para>Discovery scans <c>app/type/&lt;name&gt;/serializer/&lt;kind&gt;.cs</c>
/// — each file is a <c>public static class</c> exposing a static
/// <c>object? Read(object raw, string? kind, <see cref="ReadContext"/> ctx)</c>.
/// The file name maps to the kind token (<c>Default.cs</c> → wildcard
/// <c>"*"</c>); the parent folder name maps to the PLang type name. The exact
/// mirror of the renderer's static-<c>Write</c> scan, with <c>Write</c>→<c>Read</c>.</para>
///
/// <para><see cref="Register"/> is the runtime seam for DLLs loaded via
/// <c>code.load</c> — runtime registrations shadow generator-discovered entries,
/// same precedence as the renderer.</para>
/// </summary>
public sealed class @this
{
    /// <summary>Wildcard kind token — covers any kind the type reads uniformly. Mirror of <see cref="app.type.renderer.@this.AnyFormat"/>.</summary>
    public const string AnyKind = "*";

    /// <summary>
    /// Read-side mirror of the renderer's <c>Write(object, IWriter)</c>:
    /// turns the value's own raw source form into the materialized value,
    /// using <paramref name="kind"/> to pick the variant.
    /// </summary>
    public delegate object? Read(object raw, string? kind, ReadContext ctx);

    private readonly ConcurrentDictionary<(string Type, string Kind), Read> _generated = new();
    private readonly ConcurrentDictionary<(string Type, string Kind), Read> _runtime = new();

    private readonly object _initLock = new();
    private bool _initialized;

    /// <summary>
    /// Assemblies to scan for <c>serializer/&lt;kind&gt;.cs</c> classes.
    /// Defaults to the App assembly; consumers can extend (test fixtures).
    /// </summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    /// <summary>
    /// Returns the <see cref="Read"/> delegate registered for
    /// <paramref name="typeName"/> + <paramref name="kind"/>, falling back to
    /// the wildcard <c>"*"</c> entry, or null when neither is present. Runtime
    /// registrations win over generated entries; an exact (type, kind) match
    /// wins over the wildcard at the same level. Precedence mirrors the
    /// renderer exactly: runtime-exact → generated-exact → runtime-"*" → generated-"*".
    /// </summary>
    public Read? Of(string typeName, string? kind)
    {
        EnsureInitialized();
        var k = string.IsNullOrEmpty(kind) ? AnyKind : kind!;
        if (_runtime.TryGetValue((typeName, k), out var rt)) return rt;
        if (_generated.TryGetValue((typeName, k), out var gen)) return gen;
        if (_runtime.TryGetValue((typeName, AnyKind), out var rtAny)) return rtAny;
        if (_generated.TryGetValue((typeName, AnyKind), out var genAny)) return genAny;
        return null;
    }

    /// <summary>
    /// The type whose reader is registered under <paramref name="kind"/> — a
    /// kind-specific reader names the inner type its kind decodes to
    /// (<c>json→item</c>, <c>csv→table</c>). Wildcard (<see cref="AnyKind"/>)
    /// entries read a type uniformly across kinds, so they are not kind-specific
    /// and never answer here. Null when no kind-specific reader exists (the
    /// caller falls back to the format family). Runtime registrations win.
    /// </summary>
    public string? TypeOf(string? kind)
    {
        if (string.IsNullOrEmpty(kind)) return null;
        EnsureInitialized();
        foreach (var key in _runtime.Keys)
            if (string.Equals(key.Kind, kind, System.StringComparison.OrdinalIgnoreCase)) return key.Type;
        foreach (var key in _generated.Keys)
            if (string.Equals(key.Kind, kind, System.StringComparison.OrdinalIgnoreCase)) return key.Type;
        return null;
    }

    /// <summary>
    /// Runtime registration — for DLLs loaded via <c>code.load</c> that ship
    /// readers for runtime-registered types. Already-registered (type, kind)
    /// entries are replaced (runtime wins).
    /// </summary>
    public void Register(string typeName, string kind, Read read)
    {
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(kind) || read == null) return;
        _runtime[(typeName, kind)] = read;
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

            // namespace shape: app.type.<typeName>.serializer
            var ns = type.Namespace;
            var pivot = ns.LastIndexOf(".serializer", System.StringComparison.Ordinal);
            var head = ns[..pivot];
            var lastDot = head.LastIndexOf('.');
            if (lastDot < 0) continue;
            // Strip a leading @ — a type whose folder/namespace is a C# keyword
            // (app.type.@bool) is named without it everywhere else ("bool"), so the
            // reader must register under the bare name to be found by Readers.Of.
            var typeName = head[(lastDot + 1)..].TrimStart('@');

            // file/class name maps to kind token; "Default" → wildcard.
            var kind = type.Name.Equals("Default", System.StringComparison.Ordinal)
                ? AnyKind
                : type.Name.ToLowerInvariant();

            var method = type.GetMethod("Read",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            if (method == null) continue;
            var parameters = method.GetParameters();
            if (parameters.Length != 3) continue;
            if (parameters[2].ParameterType != typeof(ReadContext)) continue;

            // Reflection wraps a thrown exception in TargetInvocationException;
            // unwrap so callers (source.Value's parse-failure catch) see the real
            // JsonException/FormatException and author their own error, rather than
            // a reflection wrapper leaking to the courier.
            Read del = (raw, k, ctx) =>
            {
                try { return method.Invoke(null, new object?[] { raw, k, ctx }); }
                catch (System.Reflection.TargetInvocationException tie) when (tie.InnerException != null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                    throw; // unreachable — Throw() always rethrows
                }
            };

            _generated[(typeName, kind)] = del;
        }
    }

    private static IEnumerable<System.Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}
