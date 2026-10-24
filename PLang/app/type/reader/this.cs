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

    private readonly ConcurrentDictionary<(string Type, string Kind), Read> _generated = new();
    private readonly ConcurrentDictionary<(string Type, string Kind), Read> _runtime = new();

    // The typed (ITypeReader) registry — the two read modes live side by side, and
    // both are load-bearing:
    //   Typed(...) — a token-stream pull off a tokenized source (the .pr wire read,
    //     json.Reader): the type pulls its value token-by-token, no DOM.
    //   Of(...)    — a whole-payload content decode (Default.Read / csv / image):
    //     the raw string/bytes is already in hand and the type decodes it whole
    //     (CSV parse, base64, JSON-of-a-string). A token reader buys nothing here.
    // A type may answer both — its wire value streams via Typed, its content form
    // (a file's bytes materialising through source.Value) decodes via Of.
    private readonly ConcurrentDictionary<(string Type, string Kind), ITypeReader> _generatedTyped = new();
    private readonly ConcurrentDictionary<(string Type, string Kind), ITypeReader> _runtimeTyped = new();

    private readonly object _initLock = new();
    private bool _initialized;

    /// <summary>
    /// Assemblies to scan for <c>serializer/&lt;kind&gt;.cs</c> classes.
    /// Defaults to the App assembly; consumers can extend (test fixtures).
    /// </summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    // The facts of the code the readers are found in, read once per code.
    private readonly global::app.type.list.@this.Code _code;

    // The readers of the list a sub app's list is a layer over: what this one doesn't answer, they do.
    private readonly @this? _parent;

    public @this() : this(new global::app.type.list.@this.Code(), null) { }

    /// <summary>The readers of a type list, found in its code's facts; a layer's are only what its app registers, over
    /// <paramref name="parent"/>'s.</summary>
    internal @this(global::app.type.list.@this.Code code, @this? parent)
    {
        _code = code;
        _parent = parent;
        // a layer scans nothing: the shipped readers are its parent's
        if (parent != null) Assemblies.Clear();
    }

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
        if (_runtime.TryGetValue((typeName, k), out var rt)) return rt;
        if (_generated.TryGetValue((typeName, k), out var gen)) return gen;
        if (_runtime.TryGetValue((typeName, AnyKind), out var rtAny)) return rtAny;
        if (_generated.TryGetValue((typeName, AnyKind), out var genAny)) return genAny;
        return _parent?.Of(typeName, kind);
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
        if (_runtimeTyped.TryGetValue((typeName, k), out var rt)) return rt;
        if (_generatedTyped.TryGetValue((typeName, k), out var gen)) return gen;
        if (_runtimeTyped.TryGetValue((typeName, AnyKind), out var rtAny)) return rtAny;
        if (_generatedTyped.TryGetValue((typeName, AnyKind), out var genAny)) return genAny;
        return _parent?.Typed(typeName, kind);
    }

    /// <summary>
    /// The reader for a value of <paramref name="typeName"/>/<paramref name="kind"/> —
    /// narrowing <c>binary</c> content to the type its kind names (<c>json→item</c>,
    /// <c>jpg→image</c>, <c>csv→table</c>, <c>plang-goal→goal</c>). A type that ships no
    /// reader of its own reads its stored value as a record and makes itself from it
    /// (<see cref="Record"/>: the item owns how it's made) — the settings
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
        // (the item owns how it's made) — the settings classes read this way.
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
        foreach (var key in _runtime.Keys)
            if (string.Equals(key.Kind, kind, System.StringComparison.OrdinalIgnoreCase)) return key.Type;
        foreach (var key in _generated.Keys)
            if (string.Equals(key.Kind, kind, System.StringComparison.OrdinalIgnoreCase)) return key.Type;
        return _parent?.TypeOf(kind);
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

    /// <summary>The typed runtime seam — mirrors the untyped <see cref="Register(string,string,Read)"/>
    /// for an <see cref="ITypeReader"/> registered at boot (the closed choice readers) or by a
    /// <c>code.load</c> DLL shipping typed readers. Same precedence as the untyped side.</summary>
    public void Register(string typeName, string kind, ITypeReader reader)
    {
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(kind) || reader == null) return;
        _runtimeTyped[(typeName, kind)] = reader;
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

    // The readers an assembly ships under its serializer folders, from its facts: each token reader made for this
    // reader (the token-stream table), each whole-payload decode as it is.
    private void IndexAssembly(Assembly assembly)
    {
        var library = _code[assembly];
        foreach (var (type, kind, made) in library.Typed)
            _generatedTyped[(type, kind)] = (ITypeReader)made.Invoke(null);
        foreach (var (type, kind, read) in library.Read)
            _generated[(type, kind)] = read;
    }

}
