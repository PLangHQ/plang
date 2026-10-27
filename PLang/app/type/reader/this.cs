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
/// <para>Two kinds: the code's readers (<see cref="code"/>), the ones its facts ship, and a layer's
/// (<see cref="layer"/>), only what its app registers, over the readers under it. <see cref="Register"/> is the
/// runtime seam for DLLs loaded via <c>code.load</c> — runtime registrations shadow generator-discovered
/// entries.</para>
/// </summary>
public abstract class @this
{
    /// <summary>Wildcard kind token — covers any kind the type reads uniformly.</summary>
    public const string AnyKind = "*";

    /// <summary>
    /// Turns the value's own raw source form into the materialized value,
    /// using <paramref name="kind"/> to pick the variant.
    /// </summary>
    public delegate object? Read(object raw, string? kind, ReadContext ctx);

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
    // A token reader is keyed by the view it reads for too: null for every view.
    private readonly ConcurrentDictionary<(string Type, string Kind, View? View), ITypeReader> _runtimeTyped = new();

    // The readers an assembly ships under its serializer folders, from the code's facts: each token reader the code
    // made (the token-stream table), each whole-payload decode as it is. Readers that ship none hold an empty one.
    private protected sealed class shipped
    {
        internal readonly Dictionary<(string Type, string Kind), Read> Payloads = new();
        internal readonly Dictionary<(string Type, string Kind, View? View), ITypeReader> Tokens = new();

        internal shipped() { }

        internal shipped(global::app.type.list.@this.Code code, IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                var library = code[assembly];
                foreach (var (type, reader) in library.Tokens)
                    Tokens[(type, reader.Kind, reader.View)] = reader;
                foreach (var (type, kind, read) in library.Payloads)
                    Payloads[(type, kind)] = read;
            }
        }
    }

    // The readers these ship, each kind its own: the code's, made from its facts; a layer's, none.
    private protected abstract shipped Shipped { get; }

    // Readers are the code's or a layer's, made by the type list alone.
    private protected @this() { }

    /// <summary>
    /// Returns the <see cref="Read"/> delegate registered for
    /// <paramref name="typeName"/> + <paramref name="kind"/>, falling back to
    /// the wildcard <c>"*"</c> entry, or null when neither is present. Runtime
    /// registrations win over generated entries; an exact (type, kind) match
    /// wins over the wildcard at the same level. Precedence: runtime-exact → generated-exact →
    /// runtime-"*" → generated-"*", then the readers under a layer.
    /// </summary>
    public Read? Of(string typeName, string? kind) => Payload(typeName, kind);

    /// <summary>
    /// The format-agnostic pull reader registered for <paramref name="typeName"/> + <paramref name="kind"/> that reads
    /// for <paramref name="view"/> alone (null: the one that reads in every view). Same precedence as <see cref="Of"/>:
    /// runtime-exact → generated-exact → runtime-"*" → generated-"*". Null when none is registered.
    /// </summary>
    internal ITypeReader? Typed(string typeName, string? kind, View? view) => Token(typeName, kind, view);

    // The whole-payload decode these readers hold for the type and kind, in the precedence Of names.
    private protected virtual Read? Payload(string typeName, string? kind)
        => Find(_runtime, Shipped.Payloads, k => (typeName, k), kind);

    // The token reader these readers hold for the type, kind and view, in the precedence Typed names.
    private protected virtual ITypeReader? Token(string typeName, string? kind, View? view)
        => Find(_runtimeTyped, Shipped.Tokens, k => (typeName, k, view), kind);

    // The entry under the key a kind makes, in the one precedence both tables are read by: runtime-exact →
    // shipped-exact → runtime-"*" → shipped-"*".
    private T? Find<TKey, T>(ConcurrentDictionary<TKey, T> runtime, Dictionary<TKey, T> shipped,
        Func<string, TKey> key, string? kind) where TKey : notnull where T : class
    {
        var exact = key(string.IsNullOrEmpty(kind) ? AnyKind : kind!);
        var any = key(AnyKind);
        return runtime.GetValueOrDefault(exact) ?? shipped.GetValueOrDefault(exact)
            ?? runtime.GetValueOrDefault(any) ?? shipped.GetValueOrDefault(any);
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
        // A type that ships no reader of its own reads its stored value as a record and makes itself from it
        // (the item owns how it's made) — the settings classes read this way.
        => Registered(typeName, kind, context) ?? new Record(Narrowed(typeName, kind, context));

    /// <summary>The reader <paramref name="typeName"/>/<paramref name="kind"/> registers — binary content narrowed to
    /// the type its kind names first; null when the type registers none.</summary>
    internal ITypeReader? Registered(string typeName, string? kind, actor.context.@this context)
        => Typed(Narrowed(typeName, kind, context), kind, null);

    // Bytes stamped with a kind binary doesn't hold (a stamp written before the format was its type's) are the type the
    // kind names as an extension (a format's name is its extension — png, json, md, csv), never an app-wide search by
    // bare name — ONLY when that type can read them; otherwise the bytes ride as binary.
    private string Narrowed(string typeName, string? kind, actor.context.@this context)
    {
        if (!string.Equals(typeName, "binary", System.StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(kind)
            || context.App.type.list["binary"].kind[kind!] != null)
            return typeName;
        var inner = context.App.type.list.Extension(kind!, context).Name;
        return !string.Equals(inner, "binary", System.StringComparison.OrdinalIgnoreCase) && Typed(inner, kind, null) is not null
            ? inner : typeName;
    }


    /// <summary>
    /// The type whose reader is registered under <paramref name="kind"/> — a
    /// kind-specific reader names the inner type its kind decodes to
    /// (<c>json→item</c>, <c>csv→table</c>). Wildcard (<see cref="AnyKind"/>)
    /// entries read a type uniformly across kinds, so they are not kind-specific
    /// and never answer here. Null when no kind-specific reader exists (the
    /// caller falls back to the format family). Runtime registrations win.
    /// </summary>
    public string? TypeOf(string? kind) => string.IsNullOrEmpty(kind) ? null : Owner(kind!);

    // The type these readers read under a kind-specific reader of <paramref name="kind"/>.
    private protected virtual string? Owner(string kind)
    {
        foreach (var key in _runtime.Keys)
            if (string.Equals(key.Kind, kind, System.StringComparison.OrdinalIgnoreCase)) return key.Type;
        foreach (var key in Shipped.Payloads.Keys)
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

    /// <summary>The typed runtime seam — mirrors the untyped <see cref="Register(string,string,Read)"/>
    /// for an <see cref="ITypeReader"/> registered at boot (the closed choice readers) or by a
    /// <c>code.load</c> DLL shipping typed readers. Same precedence as the untyped side.</summary>
    public void Register(string typeName, string kind, ITypeReader reader)
    {
        if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(kind) || reader == null) return;
        _runtimeTyped[(typeName, kind, reader.View)] = reader;
    }


}
