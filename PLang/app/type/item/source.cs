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
    public override async System.Threading.Tasks.ValueTask<@this> Value(global::app.data.@this asking)
    {
        var read = Context?.App.Type.Readers.Of(_type, _kind);
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
            // first touch, never thrown to the courier.
            asking.Fail(new global::app.error.Error(
                $"failed to read %{asking.Name}% as {_type}{(_kind != null ? $"/{_kind}" : "")}: {ex.Message}",
                "MaterializeFailed", 400) { Exception = ex });
            return Absent;
        }

        var answer = global::app.data.@this.Lift(parsed, Context);
        if (ReferenceEquals(answer, this)) return this;
        answer.Accumulate(this);
        await System.Threading.Tasks.Task.CompletedTask;
        return answer;
    }

    public override bool IsTruthy() => _raw switch
    {
        string s => s.Length > 0,
        byte[] b => b.Length > 0,
        _ => true,
    };

    public override bool IsLeaf => true;

    /// <summary>Verbatim passthrough — the raw form streams out untouched.</summary>
    public override void Write(global::app.channel.serializer.IWriter w)
    {
        if (_raw is byte[] b) w.Bytes(b);
        else w.String(_raw.ToString() ?? "");
    }


    internal override object? Clr(System.Type target) => ClrConvert(_raw, target);

    /// <summary>Display is the raw text form; bytes show as a size note, never decoded.</summary>
    public override string ToString() => _raw is byte[] b ? $"({b.Length} bytes)" : _raw.ToString() ?? "";
}
