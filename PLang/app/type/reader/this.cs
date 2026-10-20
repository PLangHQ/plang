using System.Collections.Concurrent;
using System.Reflection;

namespace app.type.reader;

/// <summary>
/// Per-(type, kind) reader dispatch — the read side (a value writes itself). The reader decodes
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
/// <c>"*"</c>); the parent folder name maps to the PLang type name.</para>
///
/// <para><see cref="Register"/> is the runtime seam for DLLs loaded via
/// <c>code.load</c> — runtime registrations shadow generator-discovered entries.</para>
/// </summary>
public sealed class @this
{
    /// <summary>Wildcard kind token — covers any kind the type reads uniformly.</summary>
    public const string AnyKind = "*";

    /// <summary>
    /// Turns the value's own raw source form into the materialized value,
    /// using <paramref name="kind"/> to pick the variant.
    /// </summary>
    public delegate object? Read(object raw, string? kind, ReadContext ctx);

    // The code's readers — the facts' tables, shared by every app made from the code (the whole-payload reads and the
    // token readers) — and the ones registered at run time (a loaded DLL's), made on the first registration.
    private IReadOnlyDictionary<(string Type, string Kind), Read> _generated = new Dictionary<(string, string), Read>();
    private ConcurrentDictionary<(string Type, string Kind), Read>? _runtime;

    // The typed (ITypeReader) registry — the two read modes live side by side, and
    // both are load-bearing:
    //   Typed(...) — a token-stream pull off a tokenized source (the .pr wire read,
    //     json.Reader): the type pulls its value token-by-token, no DOM.
    //   Of(...)    — a whole-payload content decode (Default.Read / csv / image):
    //     the raw string/bytes is already in hand and the type decodes it whole
    //     (CSV parse, base64, JSON-of-a-string). A token reader buys nothing here.
    // A type may answer both — its wire value streams via Typed, its content form
    // (a file's bytes materialising through source.Value) decodes via Of.
    private IReadOnlyDictionary<(string Type, string Kind), ITypeReader> _generatedTyped = new Dictionary<(string, string), ITypeReader>();
    private ConcurrentDictionary<(string Type, string Kind), ITypeReader>? _runtimeTyped;

    private volatile bool _initialized;

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
    /// wins over the wildcard at the same level. Precedence: runtime-exact → generated-exact →
    /// runtime-"*" → generated-"*".
    /// </summary>
    public Read? Of(string typeName, string? kind)
    {
        EnsureInitialized();
        var k = string.IsNullOrEmpty(kind) ? AnyKind : kind!;
        var runtime = _runtime;
        if (runtime != null && runtime.TryGetValue((typeName, k), out var rt)) return rt;
        if (_generated.TryGetValue((typeName, k), out var gen)) return gen;
        if (runtime != null && runtime.TryGetValue((typeName, AnyKind), out var rtAny)) return rtAny;
        if (_generated.TryGetValue((typeName, AnyKind), out var genAny)) return genAny;
        return null;
    }

    /// <summary>
    /// Returns the <see cref="ITypeReader"/> registered for
    /// <paramref name="typeName"/> + <paramref name="kind"/> — the format-agnostic
    /// pull reader. Same precedence as <see cref="Of"/>: runtime-exact →
    /// generated-exact → runtime-"*" → generated-"*". Null when the type has not
    /// been converted to a typed reader yet (the caller falls back to <see cref="Of"/>).
    /// </summary>
    public ITypeReader? Typed(string typeName, string? kind)
    {
        EnsureInitialized();
        var k = string.IsNullOrEmpty(kind) ? AnyKind : kind!;
        var runtime = _runtimeTyped;
        if (runtime != null && runtime.TryGetValue((typeName, k), out var rt)) return rt;
        if (_generatedTyped.TryGetValue((typeName, k), out var gen)) return gen;
        if (runtime != null && runtime.TryGetValue((typeName, AnyKind), out var rtAny)) return rtAny;
        if (_generatedTyped.TryGetValue((typeName, AnyKind), out var genAny)) return genAny;
        return null;
    }

    /// <summary>
    /// The reader for a value of <paramref name="typeName"/>/<paramref name="kind"/> —
    /// narrowing <c>binary</c> content to the type its kind names (<c>json→item</c>,
    /// <c>jpg→image</c>, <c>csv→table</c>, <c>plang-goal→goal</c>). A type that ships no
    /// reader of its own reads its stored value as a record and makes itself from it
    /// (<see cref="Record"/>, decision 789: the item owns how it's made) — the settings
    /// classes read this way, and a record a type can't be made from declines loudly.
    /// </summary>
    public ITypeReader Reader(string typeName, string? kind, actor.context.@this context)
    {
        if (string.Equals(typeName, "binary", System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(kind)
            && context.App.type.list["binary"].kind[kind!] == null)
        {
            // Bytes stamped with a kind binary doesn't hold (a stamp written before the format was its
            // type's): narrow by the kind as an extension (a format's name is its extension — png, json,
            // md, csv), never an app-wide search by bare name. Narrow ONLY when the kind's type can read
            // it; otherwise the bytes ride as binary.
            var inner = context.App.type.list.Extension(kind!, context).Name;
            if (!string.Equals(inner, "binary", System.StringComparison.OrdinalIgnoreCase)
                && Typed(inner, kind) is not null) typeName = inner;
        }
        // A type that ships no reader of its own reads its stored value as a record and makes itself from it
        // (decision 789: the item owns how it's made) — the settings classes read this way.
        return Typed(typeName, kind) ?? new Record(typeName);
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
        foreach (var key in _runtime?.Keys ?? [])
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
        System.Threading.LazyInitializer.EnsureInitialized(ref _runtime)[(typeName, kind)] = read;
    }

    /// <summary>The typed runtime seam — mirrors the untyped <see cref="Register(string,string,Read)"/>
    /// for an <see cref="ITypeReader"/> registered at boot (the closed choice readers) or by a
    /// <c>code.load</c> DLL shipping typed readers. Same precedence as the untyped side.</summary>
    public void Register(string typeName, string kind, ITypeReader reader)
    {
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(kind) || reader == null) return;
        System.Threading.LazyInitializer.EnsureInitialized(ref _runtimeTyped)[(typeName, kind)] = reader;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
        lock (Assemblies)
        {
            if (_initialized) return;
            // one assembly's readers are its facts' tables as they stand, shared; several are merged for this registry,
            // a later assembly's answering over an earlier's
            var readers = Assemblies.Select(assembly => (_code?.Invoke(assembly) ?? new global::app.assembly.@this(assembly)).readers).ToList();
            if (readers.Count == 1)
            {
                _generated = readers[0].Read;
                _generatedTyped = readers[0].Typed;
            }
            else
            {
                var read = new Dictionary<(string, string), Read>();
                var typed = new Dictionary<(string, string), ITypeReader>();
                foreach (var each in readers)
                {
                    foreach (var (key, value) in each.Read) read[key] = value;
                    foreach (var (key, value) in each.Typed) typed[key] = value;
                }
                _generated = read;
                _generatedTyped = typed;
            }
            _initialized = true;
        }
    }

    // The code's facts for an assembly — the ones the type list was born holding; null reads them here.
    private readonly System.Func<Assembly, global::app.assembly.@this>? _code;

    public @this() { }

    /// <summary>A registry that reads each assembly's readers from <paramref name="code"/>: the code's facts, read once
    /// for every app.</summary>
    internal @this(System.Func<Assembly, global::app.assembly.@this> code) => _code = code;
}
