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
    private readonly object _raw;
    private readonly string _type;
    private readonly string? _kind;
    private readonly bool _strict;

    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    public source(object raw, string typeName, string? kind, bool strict = false)
    {
        _raw = raw ?? throw new System.ArgumentNullException(nameof(raw));
        _type = string.IsNullOrWhiteSpace(typeName) ? "item" : typeName;
        _kind = kind;
        _strict = strict;
    }

    /// <summary>The undecoded source form — <c>string</c> or <c>byte[]</c>.</summary>
    public object Raw => _raw;

    /// <summary>The declared judgement, verbatim — the source IS the declared
    /// type, unparsed.</summary>
    protected internal override global::app.type.@this Mint()
        => new(_type, _kind, _strict);

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
        if (_raw is byte[] b && _type.ToLowerInvariant() is "text")
        {
            try { return new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(b); }
            catch (System.Text.DecoderFallbackException) { return b; }
        }
        return _raw;
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

    public override async System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this asking)
    {
        var read = Context?.App.Type.Readers.Of(_type, _kind);
        // A binary form off I/O carries only its kind; the kind names the type it
        // narrows to, and that type's reader does the parse (json→item, jpg→image).
        if (read == null && _kind != null && Context != null)
        {
            string inner = new global::app.type.kind.@this(_kind, Context).Type.Name;
            read = Context.App.Type.Readers.Of(inner, _kind);
        }
        object? parsed;
        try
        {
            if (read != null)
            {
                parsed = read(_raw, _kind, new global::app.type.reader.ReadContext(Context));
            }
            else if (_raw is string s)
            {
                // No reader — a string raw with a known type reads via the type's own
                // Convert (json→dict, WireReader, primitive coercion).
                var entity = global::app.type.@this.Create(_type, _kind, context: Context);
                parsed = entity.Convert(s);
            }
            else if (_raw is byte[] bytes && string.Equals(_type, "binary", System.StringComparison.OrdinalIgnoreCase))
            {
                // A raw byte form typed plainly `binary` (no domain type, no kind) IS
                // a binary value — its face is its bytes; surface it as binary rather
                // than leaving the source shell. A byte form with a SPECIFIC type/kind
                // awaiting a reader (table/xlsx, image/heic) stays raw below until its
                // reader exists.
                parsed = new global::app.type.binary.@this(bytes);
            }
            else
            {
                return this;
            }
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
            asking.Fail(new global::app.error.Error(
                $"failed to read %{asking.Name}% as {_type}{(_kind != null ? $"/{_kind}" : "")}: {ex.Message}{where}",
                "MaterializeFailed", 400) { Exception = ex });
            return Absent;
        }

        var answer = global::app.type.@this.Create(parsed, Context);
        if (ReferenceEquals(answer, this)) return this;
        answer.Accumulate(this);
        await System.Threading.Tasks.Task.CompletedTask;
        return answer;
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

    public override bool IsTruthy() => _raw switch
    {
        string s => s.Length > 0,
        byte[] b => b.Length > 0,
        _ => true,
    };

    public override bool IsLeaf => true;

    /// <summary>
    /// Verbatim passthrough — the raw form streams out untouched. A json-shaped raw
    /// (object/item carrying <c>kind:json</c>, or a <c>number</c> literal) rides inline
    /// and UNQUOTED via <see cref="global::app.channel.serializer.IWriter.Raw"/>; any
    /// other raw string is a quoted string; bytes are base64. The declared
    /// <c>{type, kind}</c> decides — not a content sniff. (Owns what Wire.Write's
    /// EmitRawVerbatim used to do, so RawUntouched routes through data.Output.)
    /// </summary>
    public override void Write(global::app.channel.serializer.IWriter w)
    {
        if (_raw is byte[] b) { w.Bytes(b); return; }
        var s = _raw.ToString() ?? "";
        bool isJson = (_type is "object" or "item" && string.Equals(_kind, "json", System.StringComparison.OrdinalIgnoreCase))
                      || _type == "number";
        if (isJson) w.Raw(s); else w.String(s);
    }


    internal override object? Clr(System.Type target) => ClrConvert(_raw, target);

    /// <summary>Display is the raw text form; bytes show as a size note, never decoded.</summary>
    public override string ToString() => _raw is byte[] b ? $"({b.Length} bytes)" : _raw.ToString() ?? "";
}
