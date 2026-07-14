namespace app.type.item.base64;

/// <summary>
/// PLang <c>base64</c> value — an encoded string payload, possibly content-tagged
/// (a data-url's mime rides as Kind). String face = the payload; byte face
/// (<see cref="RawBytes"/>) = the decoded bytes. NOT a binary subtype: binary is
/// raw bytes, base64 is an encoding.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "SGVsbG8=";
    public static string Shape => "string";
    public static string Description =>
        "A base64-encoded payload (REST binary fields, data-urls). `as base64` ENCODES the "
        + "source value (lazily); a field/param typed base64 validates its payload at read. "
        + "Kind carries the content family from a data-url mime (gif, png, json, ...).";

    // THE backing — private, per text's discipline. Null only while a held source
    // awaits its encode at the Value door.
    private string? _value;
    // The item to encode — held WHOLE at construction (laziness is state, not a
    // method); resolved once at the Value door then RELEASED (the encode is cached,
    // so nothing keeps the source's bytes alive). Null when born of a payload.
    private global::app.type.item.@this? _source;

    /// <summary>Content-family token off a data-url mime ("gif", "json"); null for a bare payload.</summary>
    public string? Kind { get; init; }

    protected internal override global::app.type.@this Type
        => new("base64", typeof(string)) { Kind = Kind is { } k ? new global::app.type.kind.@this(k) : null };

    public @this(string value) { _value = value; }
    private @this(global::app.type.item.@this source) { _source = source; }

    /// <summary>
    /// The one string-parse home, shared by the pure core and the wire reader: a
    /// <c>data:&lt;mime&gt;;base64,&lt;payload&gt;</c> unwraps (mime tail → Kind); a bare
    /// string must BE valid base64 — this is the validate door, never an encode.
    /// THROWS <see cref="System.FormatException"/> on anything malformed (no data.Fail
    /// in scope here; the born path's source.Value catch turns it into MaterializeFailed).
    /// </summary>
    internal static @this Parse(string raw)
    {
        if (raw.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
        {
            var comma = raw.IndexOf(',');
            if (comma < 5) throw new System.FormatException(
                "malformed data-url — no ',' separating header from payload.");
            var header = raw[5..comma];                       // e.g. image/gif;base64
            if (!header.EndsWith(";base64", System.StringComparison.OrdinalIgnoreCase))
                throw new System.FormatException(
                    "data-url is not ;base64-encoded — only base64 data-urls are a base64 value.");
            var payload = raw[(comma + 1)..];
            if (!System.Buffers.Text.Base64.IsValid(payload))
                throw new System.FormatException("data-url payload is not valid base64.");
            var mime = header[..^7];
            var slash = mime.IndexOf('/');
            var kind = slash >= 0 ? mime[(slash + 1)..] : null;
            return new @this(payload) { Kind = kind == "octet-stream" ? null : kind };
        }
        if (!System.Buffers.Text.Base64.IsValid(raw))
            throw new System.FormatException("not a valid base64 payload.");
        return new @this(raw);
    }

    /// <summary>THE PURE CORE — pass-through; a string parses (data-url or valid payload;
    /// malformed throws per the error policy). Serves comparison coercion; the courier
    /// below owns the encode semantics. A non-string declines (type mismatch, not an error).</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value is string s ? Parse(s) : null;
    }

    /// <summary>The courier — <c>as base64</c> / a typed slot fed from memory ALWAYS ENCODES:
    /// any item is held whole and encodes at the Value door. The one exception is a string
    /// face holding a data-url — an explicit unwrap ask, not content to encode. A value that
    /// already IS base64 arrives typed via the reader, never through here.</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (value is @this self) return self;
        var s = value as string ?? (value as global::app.type.item.text.@this)?.ToString();
        if (s != null && s.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
        {
            try { return Parse(s); }
            catch (System.FormatException ex)
            {
                data.Fail(new global::app.error.Error(ex.Message, "Base64Invalid", 400));
                return null;
            }
        }
        // A raw CLR string (the entity door's leaf-retype lowers text before calling here)
        // has no door to defer to — content in hand, encode now.
        if (value is string raw)
            return new @this(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw)));
        // ANY item — text, image, dict, binary — is held whole; ONE lazy path, the door encodes.
        if (value is global::app.type.item.@this it) return new @this(it);
        data.Fail(new global::app.error.Error(
            $"Cannot create base64 from {value?.GetType().Name ?? "null"}.", "Base64ConversionFailed", 400));
        return null;
    }

    /// <summary>
    /// The encode door. A held source materializes through ITS door, then encodes: its own
    /// byte face (<see cref="RawBytes"/>) when it has one, else its bare json wire via the
    /// actor's registered serializer. Once, cached — mirrors image's load-at-Value.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(
        global::app.data.@this data)
    {
        if (_value != null || _source == null) return this;
        var ready = await _source.Value(data);
        if (!data.Success) return Absent;
        byte[] bytes;
        if (ready.RawBytes is { } raw) bytes = raw;
        else
        {
            using var ms = new System.IO.MemoryStream();
            var carrier = new global::app.data.@this("", ready, context: data.Context);
            var written = await data.Context.Actor.Channel.Serializers.Json
                .SerializeAsync(ms, carrier, global::app.View.Out);
            if (!written.Success)
            {
                data.Fail(written.Error ?? new global::app.error.Error(
                    "could not serialize value for base64 encode.", "Base64EncodeFailed", 400));
                return Absent;
            }
            bytes = ms.ToArray();
        }
        _value = System.Convert.ToBase64String(bytes);
        _source = null;   // encoded + cached — let the source's bytes collect
        return this;
    }

    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(_value ?? "");
    public override string ToString() => _value ?? "";
    public override string? RawText => _value;

    /// <summary>base64's byte face IS its decoded bytes — the type's whole meaning
    /// (string face = encoded, byte face = decoded).</summary>
    public override byte[]? RawBytes => _value is { } v ? System.Convert.FromBase64String(v) : null;

    /// <summary>Empty payload (and nothing pending encode) is falsy.</summary>
    public override bool IsTruthy() => !string.IsNullOrEmpty(_value) || _source != null;

    public override global::app.type.item.@this Kinded(string? kind)
        => _value is { } v ? new @this(v) { Kind = kind } : this;

    /// <summary>CLR exit — a byte[] target gets the DECODED bytes; anything else the payload string.</summary>
    internal override object? Clr(System.Type target)
        => target == typeof(byte[]) && _value is { } v
            ? System.Convert.FromBase64String(v)
            : ClrConvert(_value, target);

    /// <summary>Between text (100) and binary (250): drives a text compare (payload identity,
    /// case-SENSITIVE — base64 is); binary drives byte equality (already works today: binary's
    /// core decodes our payload string after the unwrap).</summary>
    public override int Rank => 200;

    /// <summary>Payload identity, ordinal. A non-coercible/malformed other is Incomparable,
    /// not an error — the compare-local catch per the error policy.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(
        global::app.type.item.@this other)
    {
        var b = other as @this;
        if (b is null)
        {
            try { b = Create(other); }
            catch (System.FormatException) { return new(global::app.data.Comparison.Incomparable); }
        }
        if (b?._value is null || _value is null) return new(global::app.data.Comparison.Incomparable);
        var c = string.CompareOrdinal(_value, b._value);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }
}
