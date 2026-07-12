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
    // The declaration, WHOLE — the source IS this type, unparsed. Born with it at Create,
    // never shredded into Name/Kind/Strict/Template scalars and reassembled (Mint answers it
    // directly). Name/Kind/Strict/Template all read off this one object.
    private readonly global::app.type.@this _type;
    // The serializer whose encoding these bytes are in — the .pr wire is
    // application/plang, a channel's content is its own mimetype. At .Value() the
    // source selects this serializer and asks it to read the bytes (it makes the
    // matching reader; the type pulls itself off it). A capture-site fact (not the
    // type's), so it rides its own field — preserved across a re-birth like Raw.
    private readonly string _format;

    // A full-match %ref% (`%!data%`, `%messages%`) is a REFERENCE, not content — decided ONCE
    // at birth: the raw form and the authored-template flag are immutable, so reading it back is
    // a bool check, never a per-read regex (references re-resolve every read — Cacheable=false).
    // When true, _varName holds the bare name to resolve through Variable.Get at .Value().
    public override bool IsVariable { get; }

    /// <inheritdoc/>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this?> Get(actor.context.@this ctx)
        => await ctx.Variable.Get((string)_value);   // _value is the raw "%!data%"; the store strips the %

    // Born WITH context — a source is minted only by the type entity's Create door (the sole birth
    // site), which always has a wired scope; the context-less births (the "Judge" phase) are gone.
    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this Context { get; set; } = null!;

    /// <summary>Born from a declared type entity + a raw form — the source-maker the entity's
    /// <c>Create</c> door shares. The source holds the declaration WHOLE (Name/Kind/Strict/Template
    /// all live on it); the wire may override the format (else the type derives it from the raw).</summary>
    public source(object value, global::app.type.@this type, actor.context.@this context, string? format = null)
    {
        _value = value ?? throw new System.ArgumentNullException(nameof(value));
        Context = context ?? throw new System.ArgumentNullException(nameof(context));
        _type = type ?? throw new System.ArgumentNullException(nameof(type));
        _format = format ?? type.RawFormat(value, context);
        // Full-match %ref% on ANY declared type is a reference to a binding — resolved by name at
        // .Value(), never parsed through the type reader. Trust the builder's template flag (on the
        // declaration), not the content: a structural string the builder did not mark stays literal
        // content. A BUILD-TIME security gate — content that merely looks like "%x%" must NOT
        // auto-resolve to a variable; only a builder-marked template does. Decided ONCE at birth.
        if (type.Template != null && value is string reference
            && global::app.data.@this.TryFullVarMatch(reference, out _))
        {
            IsVariable = true;
        }
    }

    /// <summary>The undecoded source form — <c>string</c> or <c>byte[]</c>.</summary>
    public object Raw => _value;

    /// <summary>The wire format the raw was captured in — birth state, preserved across a re-birth.</summary>
    public string Format => _format;

    /// <summary>The raw string face, when the source carries text (not bytes).</summary>
    public override string? RawText => _value as string;

    /// <summary>The declared judgement, verbatim — the source IS the declared type, unparsed.
    /// Held whole since birth; answered directly, not reassembled from scalars.</summary>
    protected internal override global::app.type.@this Type => _type;

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
        if (_value is byte[] b && _type.Name.ToLowerInvariant() is "text")
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
    public override bool Cacheable => _type.Template == null;

    public override async System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this data)
    {
        // A full-match %ref% names a binding — hand back what it holds through its OWN door,
        // never parse the name through the declared type's reader (a `list` reader on the string
        // "%!data%" has nothing to read). Same resolve door as text's full-match / variable.@this;
        // the declared {type,kind} was only the pre-resolution label. IsVariable decided at birth.
        if (IsVariable)
        {
            var resolved = await Get(Context);
            // Get returns a NotFound Data (IsInitialized == false) on a miss, never null for a
            // reference. A variable set to null IS initialized, so it resolves (to null); only a
            // genuinely-absent name fails here.
            if (resolved is null || !resolved.IsInitialized)
            {
                data.Fail(new global::app.error.Error(
                    $"%{_value}% is not set — nothing to answer for it.", "VariableNotFound", 404));
                return Absent;
            }
            return await resolved.Value();
        }

        await System.Threading.Tasks.Task.CompletedTask;
        try
        {
            var item = Read();
            if (ReferenceEquals(item, this)) return this;
            // Container round-trip guard: a value DECLARED a container (dict/list) that
            // materialized to a non-container leaf is a round-trip loss (a json object that
            // came back an opaque scalar) — fail LOUD here, at the point of loss, not three
            // hops later as a null navigation. Rides the catch below → MaterializeFailed,
            // named to the binding. (The apex object/item is excluded — a scalar CAN be
            // declared object; only genuine containers must stay containers.)
            if (_type.Name.ToLowerInvariant() is "dict" or "list"
                && item is not (global::app.type.item.dict.@this or global::app.type.item.list.@this or global::app.type.clr.@this))
                throw new System.InvalidOperationException(
                    $"a '{_type.Name}' value materialized to a non-container ({item.GetType().Name}) — round-trip loss");
            item.list.Add(this);
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
                $"failed to read %{data.Name}% as {_type.Name}{(_type.Kind?.Name is { } k ? $"/{k}" : "")}: {ex.Message}{where}",
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
        if (Context.Actor?.Channel.Serializers is { } serializers)
            return serializers[_format].Read(this, new global::app.type.reader.ReadContext(Context, _type.Template));
        // Context is guaranteed (born-in-ctor); reaching here means its Actor/Channel isn't
        // wired yet. Surface it as a clean MaterializeFailed (source.Value catches this), not a
        // silent unparsed return.
        throw new System.InvalidOperationException(
            $"source declared '{_type.Name}' reached read before its actor channel was wired.");
    }

    /// <summary>
    /// Navigation is first-touch: a source is still its raw form (bytes / json text),
    /// so it parses itself via the Data door — which caches the parsed value back onto
    /// <paramref name="parent"/> (a <c>%cfg%</c> source BECOMES the dict, parse-once) —
    /// then navigates the parsed value. A bad parse fails <paramref name="parent"/> with
    /// <c>MaterializeFailed</c> (the declared <c>{type, kind}</c> could not be built from
    /// the raw form); surface that, not a misleading NotFound.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
    {
        var materialized = await parent.Value();
        if (!parent.Success) return parent;
        return await materialized.Get(parent, key);
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
