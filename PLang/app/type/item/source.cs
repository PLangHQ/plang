namespace app.type.item;

/// <summary>
/// A born-with-bytes value — the undecoded source form (<c>string</c> for a
/// text source, <c>byte[]</c> for a binary one) carrying its declared
/// <c>{type, kind}</c> judgement (stamped by the channel boundary from mime, or
/// by the wire reader). It IS the declared type, unparsed: <see cref="Mint"/>
/// answers the declaration, <see cref="Peek"/> answers the raw form, and
/// <see cref="Ready"/> parses through the reader registry for (type, kind) —
/// the holding <c>Data</c> rebinds to the parsed answer, this source riding its
/// prior chain. Verbatim passthrough (serialize-without-parse) reads
/// <see cref="Raw"/> as long as no parse happened.
/// </summary>
public sealed class source : @this, module.IContext
{
    private readonly object _value;
    private readonly string _type;
    private readonly string? _kind;
    private readonly bool _strict;
    // The serializer whose encoding these bytes are in — the .pr wire is
    // application/plang, a channel's content is its own mimetype. At .Value() the
    // source selects this serializer and asks it to read the bytes (it makes the
    // matching reader; the type pulls itself off it).
    private readonly string _format;
    // The authored-content mode the bytes were read in ("plang" for a developer-authored
    // goal/.pr, null for runtime ingest). Rides into the reader's ReadContext so a %ref%
    // leaf borns a live template; the trust is the reader's mode, never the content.
    private readonly string? _template;

    // Context stays nullable until the context-less source births (Judge) are removed — the Judge
    // phase. (WireLocal is gone.) Then this goes non-null + born-in-ctor.
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    public source(object value, string typeName, string? kind, bool strict = false,
        string format = "text/plain", string? template = null)
    {
        _value = value ?? throw new System.ArgumentNullException(nameof(value));
        _type = string.IsNullOrWhiteSpace(typeName) ? "item" : typeName;
        _kind = kind;
        _strict = strict;
        _format = format;
        _template = template;
    }

    /// <summary>The undecoded source form — <c>string</c> or <c>byte[]</c>.</summary>
    public object Raw => _value;

    /// <summary>The declared judgement, verbatim — the source IS the declared
    /// type, unparsed.</summary>
    protected internal override global::app.type.@this Mint()
        => new(_type, _kind, _strict, _template);

    /// <summary>
    /// In memory now = the raw source form. A byte raw declared <c>text</c> decodes
    /// to its text face — that is the declaration speaking, not a content guess. A
    /// <c>binary</c>/<c>bytes</c> raw STAYS bytes: peeking never sniffs "is this valid
    /// UTF-8?" (access-driven resolution; binary's face is its bytes — decode to text
    /// is the explicit <c>as text</c>). A structured declaration (table/xlsx,
    /// image/png) keeps its bytes — the parse stays behind <see cref="Ready"/>.
    /// </summary>
    public override object? Peek()
    {
        if (_value is byte[] b && _type.ToLowerInvariant() is "text")
        {
            try { return new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(b); }
            catch (System.Text.DecoderFallbackException) { return b; }
        }
        return _value;
    }

    /// <summary>Shared by reference — a source is immutable (its raw + declared
    /// judgement are readonly; Ready parses into a NEW instance rather than
    /// mutating), and it carries a Context that points back into the App graph,
    /// so a deep clone would walk the whole runtime and could overflow. Sharing
    /// the instance is safe precisely because nothing mutates it.</summary>
    protected internal override @this Clone() => this;

    /// <summary>
    /// The parse: raw → value via the reader registry for (type, kind), falling
    /// back to the type's own <c>Convert</c> for a string raw. The answer is a
    /// new instance with this source as its prior; a raw with no reader and no
    /// type answers itself (the bytes are the value).
    /// </summary>
    /// <summary>Never final — the door parses the raw form into its value on read.</summary>
    internal override bool IsFinal => false;

    /// <summary>A template-bearing source re-resolves every read (its %refs% can change) — never
    /// cached by the holding Data; a plain source parses once and caches. Mirrors text/dict/list.</summary>
    public override bool Cacheable => _template == null;

    public override async System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this data)
    {
        await System.Threading.Tasks.Task.CompletedTask;
        try
        {
            var item = Read();
            if (ReferenceEquals(item, this)) return this;
            item.Accumulate(this);
            // Resolve the materialized item — a template (text/dict/list) renders against live
            // variables; a plain leaf/container answers itself. The source layer is transparent:
            // "give me the value" returns the value, not an intermediate unrendered template.
            return await item.Value(data);
        }
        catch (System.Exception ex) when (ex is System.Text.Json.JsonException or System.FormatException or System.InvalidOperationException)
        {
            // SOURCE authors its own failure story — the declared form did not parse
            // as its declared {type, kind}. Keyed MaterializeFailed and naming the
            // binding so navigation / set / read all surface one actionable error at
            // first touch, never thrown to the courier. A JsonException carries the JSON
            // path + line STJ stamped (which slot in WHICH .pr, at what line) — append it
            // so a malformed .pr is pinpointed, not just named.
            var where = ex is System.Text.Json.JsonException je && (je.Path != null || je.LineNumber != null)
                ? $" [at {je.Path ?? "?"}, line {je.LineNumber?.ToString() ?? "?"}]"
                : "";
            data.Fail(new global::app.error.Error(
                $"failed to read %{data.Name}% as {_type}{(_kind != null ? $"/{_kind}" : "")}: {ex.Message}{where}",
                "MaterializeFailed", 400) { Exception = ex });
            return Absent;
        }
    }

    // The dispatch: the serializer whose encoding these bytes are in reads them — it makes the
    // matching (type, kind) reader (json for the .pr wire, a value reader for text content) and
    // the type pulls itself off it. One door, every type — the source never makes a reader, never
    // names a format. (source.Value owns the try/catch + the binding-named failure story.)
    private global::app.type.item.@this Read()
    {
        if (Context?.Actor?.Channel.Serializers is { } serializers)
            return serializers[_format].Read(this, new global::app.type.reader.ReadContext(Context, _template));
        // A source is always born WITH a context — the data ctor and FromRaw both stamp it,
        // and the context-less Judge birth is gone. Reaching here means a source escaped that
        // invariant; surface it as a clean MaterializeFailed (source.Value catches this), not a
        // silent unparsed return or a born-without-context crash from a context-less Create.
        throw new System.InvalidOperationException(
            $"source declared '{_type}' reached read without a context — a source must be born with one.");
    }

    /// <summary>
    /// Navigation is first-touch: a source is still its raw form (bytes / json text),
    /// so it parses itself via the Data door — which caches the parsed value back onto
    /// <paramref name="parent"/> (a <c>%cfg%</c> source BECOMES the dict, parse-once) —
    /// then navigates the parsed value. A bad parse fails <paramref name="parent"/> with
    /// <c>MaterializeFailed</c> (the declared <c>{type, kind}</c> could not be built from
    /// the raw form); surface that, not a misleading NotFound.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Navigate(
        global::app.data.@this parent, string key)
    {
        var materialized = await parent.Value();
        if (!parent.Success) return parent;
        return await materialized.Navigate(parent, key);
    }

    public override bool IsTruthy() => _value switch
    {
        string s => s.Length > 0,
        byte[] b => b.Length > 0,
        _ => true,
    };

    public override bool IsLeaf => true;

    /// <summary>
    /// Verbatim passthrough — the raw form streams out untouched, byte-for-byte the bytes
    /// that came in (so an untouched relay's signature still verifies). Bytes are base64.
    /// The format the bytes were captured in decides the shape: a value/text format holds
    /// the value's own CONTENT (text, a path, a biginteger's digits) → a quoted string;
    /// every other (json-encoding) format holds JSON (a dict/list/number/bool slot) → it
    /// rides inline and UNQUOTED via <see cref="global::app.channel.serializer.IWriter.Raw"/>.
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter w)
    {
        if (_value is byte[] b) { w.Bytes(b); return; }
        var s = _value.ToString() ?? "";
        if (string.Equals(_format, global::app.channel.serializer.Text.Mime, System.StringComparison.OrdinalIgnoreCase))
            w.String(s);
        else
            w.Raw(s);
    }


    internal override object? Clr(System.Type target) => ClrConvert(_value, target);

    /// <summary>Display is the raw text form; bytes show as a size note, never decoded.</summary>
    public override string ToString() => _value is byte[] b ? $"({b.Length} bytes)" : _value.ToString() ?? "";
}
