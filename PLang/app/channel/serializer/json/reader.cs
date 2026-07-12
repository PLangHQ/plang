using System.Text.Json;

namespace app.channel.serializer.json;

/// <summary>
/// JSON implementation of <see cref="IReader"/> — a <c>ref struct</c> holding a
/// <c>Utf8JsonReader</c> by value (a ref struct may contain a ref struct field;
/// a <em>ref field</em> to a ref struct is illegal, CS9050). Because the reader is
/// embedded by value, advances persist only while the <see cref="json.Reader"/>
/// itself is threaded <b>by ref</b> down the read chain
/// (<c>ITypeReader.Read&lt;TReader&gt;(ref TReader, …)</c>) — the single embedded
/// cursor is shared through the ref. At the STJ bridge a converter copies its
/// <c>Utf8JsonReader</c> in (the ctor) and copies the advanced state back out
/// (<see cref="Inner"/>) so STJ's own position stays in sync.
///
/// <para>Stack-only and never boxed: a type pulls from it through the generic
/// read seam, monomorphized to this concrete reader at the call site.</para>
/// </summary>
public ref struct Reader : IReader
{
    private Utf8JsonReader _r;
    // The bytes the reader is reading over, when this reader OWNS them (the entry path).
    // With it, RawValue() slices a structured value's sub-tree straight off the buffer — no
    // DOM. Null when STJ drives the read (nested Data): RawValue() falls back to DOM there.
    private readonly byte[]? _buffer;

    public Reader(Utf8JsonReader r) => _r = r;
    public Reader(Utf8JsonReader r, byte[]? buffer) { _r = r; _buffer = buffer; }

    /// <summary>
    /// The embedded reader, by ref — to copy the advanced position back into the
    /// STJ converter's own <c>Utf8JsonReader</c> after driving a value read, and
    /// to delegate a sub-entity that still owns an STJ <c>JsonConverter</c> (the
    /// <c>type</c> descriptor, <c>path</c>) without desyncing the single pass.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.UnscopedRef]
    internal ref Utf8JsonReader Inner => ref _r;

    public string Format => "json";

    public TokenKind Peek() => _r.TokenType switch
    {
        JsonTokenType.Number => TokenKind.Number,
        JsonTokenType.String => TokenKind.String,
        JsonTokenType.True or JsonTokenType.False => TokenKind.Bool,
        JsonTokenType.Null => TokenKind.Null,
        JsonTokenType.StartArray => TokenKind.Array,
        JsonTokenType.StartObject => TokenKind.Object,
        _ => throw new JsonException($"json.Reader.Peek: not positioned at a value token ({_r.TokenType})"),
    };

    public bool Null() => _r.TokenType == JsonTokenType.Null;
    public bool Bool() => _r.GetBoolean();
    public int Int() => _r.GetInt32();
    public long Long() => _r.GetInt64();
    // Natural precision: long when it fits as an integer, else double — the cast
    // to object keeps the integer from widening to a float (a bare ?: would unify).
    public object Number() => _r.TryGetInt64(out var l) ? l : _r.GetDouble();
    public float Float() => _r.GetSingle();
    public double Double() => _r.GetDouble();
    public decimal Decimal() => _r.GetDecimal();
    public string String() => _r.GetString() ?? "";
    public byte[] Bytes() => _r.GetBytesFromBase64();
    public System.DateTime DateTime() => _r.GetDateTime();
    public System.DateTimeOffset DateTimeOffset() => _r.GetDateTimeOffset();
    // The writer emits TimeSpan via ToString("c"); read it back the same way.
    public System.TimeSpan TimeSpan() => System.TimeSpan.ParseExact(
        _r.GetString() ?? "", "c", System.Globalization.CultureInfo.InvariantCulture);
    public System.Guid Guid() => _r.GetGuid();

    public void BeginArray()
    {
        if (_r.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"json.Reader.BeginArray: expected StartArray, got {_r.TokenType}");
    }

    public bool NextElement()
    {
        _r.Read();
        return _r.TokenType != JsonTokenType.EndArray;
    }

    public void EndArray()
    {
        if (_r.TokenType != JsonTokenType.EndArray)
            throw new JsonException($"json.Reader.EndArray: expected EndArray, got {_r.TokenType}");
    }

    public void BeginObject()
    {
        if (_r.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"json.Reader.BeginObject: expected StartObject, got {_r.TokenType}");
    }

    public bool NextName(out string name)
    {
        _r.Read();
        if (_r.TokenType == JsonTokenType.EndObject) { name = ""; return false; }
        if (_r.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"json.Reader.NextName: expected PropertyName, got {_r.TokenType}");
        name = _r.GetString()!;
        _r.Read();   // position on the member value's first token
        return true;
    }

    public void EndObject()
    {
        if (_r.TokenType != JsonTokenType.EndObject)
            throw new JsonException($"json.Reader.EndObject: expected EndObject, got {_r.TokenType}");
    }

    public void Skip() => _r.Skip();

    /// <summary>
    /// The current value's raw bytes, off the reader — no DOM. A string is its UTF-8
    /// content (unescaped); a number/bool/null is the literal off the token; a structured
    /// value (object/array) is sliced from the owned buffer (token start → past Skip). Only
    /// the buffer-less STJ-nested case falls back to a single JsonDocument round-trip.
    /// </summary>
    public byte[] RawValue()
    {
        switch (_r.TokenType)
        {
            case JsonTokenType.String:
                return System.Text.Encoding.UTF8.GetBytes(_r.GetString() ?? "");
            case JsonTokenType.Null:
                return "null"u8.ToArray();
            case JsonTokenType.Number:
            case JsonTokenType.True:
            case JsonTokenType.False:
                return _r.HasValueSequence
                    ? System.Buffers.BuffersExtensions.ToArray(_r.ValueSequence)
                    : _r.ValueSpan.ToArray();
            default:   // StartObject / StartArray — a structured sub-tree
                if (_buffer != null)
                {
                    int start = (int)_r.TokenStartIndex;
                    _r.Skip();
                    return _buffer.AsSpan(start, (int)_r.BytesConsumed - start).ToArray();
                }
                using (JsonDocument doc = JsonDocument.ParseValue(ref _r))
                    return System.Text.Encoding.UTF8.GetBytes(doc.RootElement.GetRawText());
        }
    }

    /// <summary>
    /// The current value's VERBATIM document bytes — the raw token span exactly as it
    /// appears, quotes and escapes intact (<c>"line1\nline2"</c> comes back quoted, not
    /// decoded). Unlike <see cref="RawValue"/> (which decodes a string token to its
    /// content), this is byte-identical to the source, so a <c>wire</c> slice written back
    /// verbatim keeps the document's — and its signature's — bytes. Structured tokens skip
    /// to their matching close; scalars are already fully read. Needs the owned buffer: the
    /// STJ-nested read (buffer-less) has no verbatim span, so it throws LOUDLY rather than
    /// silently normalizing through a JsonDocument round-trip.
    /// </summary>
    public byte[] Slice()
    {
        if (_buffer == null)
            throw new JsonException(
                "json.Reader.Slice: verbatim capture needs the owned buffer, but this reader "
                + "was created without one (STJ-nested path). A wire slice cannot be taken here — "
                + "route the read through the buffered entry.");
        int start = (int)_r.TokenStartIndex;
        if (_r.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
            _r.Skip();   // advance past the matching close; scalars are already fully read
        return _buffer.AsSpan(start, (int)_r.BytesConsumed - start).ToArray();
    }
}
