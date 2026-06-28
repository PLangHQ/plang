namespace app.channel.serializer.value;

using TokenKind = global::app.channel.serializer.TokenKind;

/// <summary>
/// A format-neutral <see cref="IReader"/> over a single already-decoded value
/// (a <c>string</c> path/csv/source-text, or a <c>byte[]</c> blob) — the sibling of
/// <see cref="app.channel.serializer.json.Reader"/> for a value that is already in
/// hand rather than a wire stream. It yields that value as ONE scalar token, so a
/// type's <see cref="app.type.reader.ITypeReader"/> can pull its own content
/// (<c>reader.String()</c>, <c>reader.Bytes()</c>) without a wire parser — the read
/// path that materializes a <c>source</c>'s held value.
///
/// <para>Scalar-only by design: it is NOT a parser. The structural pulls
/// (<see cref="BeginObject"/>/<see cref="BeginArray"/>/…) throw — a structured value
/// (object/item/dict/list) whose value is encoded text needs a real format reader,
/// not this. The caller routes those to the parser; everything else rides here.</para>
/// </summary>
public struct Reader : IReader
{
    private readonly object _value;

    public Reader(object value) => _value = value;

    public string Format => "value";

    public TokenKind Peek() => _value switch
    {
        null => TokenKind.Null,
        bool => TokenKind.Bool,
        sbyte or byte or short or ushort or int or uint or long or ulong
            or float or double or decimal => TokenKind.Number,
        _ => TokenKind.String,
    };

    public bool Null() => _value is null;

    public bool Bool() => _value is bool b ? b : bool.Parse(Str());
    public int Int() => _value is int i ? i : int.Parse(Str(), Inv);
    public long Long() => _value is long l ? l : long.Parse(Str(), Inv);
    public object Number() => _value is string s
        ? (long.TryParse(s, Inv, out var n) ? n : double.Parse(s, Inv))
        : _value;
    public float Float() => _value is float f ? f : float.Parse(Str(), Inv);
    public double Double() => _value is double d ? d : double.Parse(Str(), Inv);
    public decimal Decimal() => _value is decimal m ? m : decimal.Parse(Str(), Inv);
    public string String() => Str();
    public byte[] Bytes() => _value is byte[] b ? b : System.Convert.FromBase64String(Str());
    public System.DateTime DateTime() => System.DateTime.Parse(Str(), Inv);
    public System.DateTimeOffset DateTimeOffset() => System.DateTimeOffset.Parse(Str(), Inv);
    public System.TimeSpan TimeSpan() => System.TimeSpan.Parse(Str(), Inv);
    public System.Guid Guid() => System.Guid.Parse(Str());

    /// <summary>The value's own bytes — its string UTF-8, or the blob itself.</summary>
    public byte[] RawValue() => _value is byte[] b ? b : System.Text.Encoding.UTF8.GetBytes(Str());

    public void Skip() { }

    // Structural pulls — a single value is not a container. A structured value
    // (encoded object/array) must go through a format parser, never this reader.
    public void BeginArray() => throw Structural();
    public bool NextElement() => throw Structural();
    public void EndArray() => throw Structural();
    public void BeginObject() => throw Structural();
    public bool NextName(out string name) => throw Structural();
    public void EndObject() => throw Structural();

    // A byte form decodes to its UTF-8 text (csv/source/path content arrives as bytes);
    // a string is itself; anything else is its invariant text.
    private string Str() => _value switch
    {
        string s => s,
        byte[] b => System.Text.Encoding.UTF8.GetString(b),
        _ => _value?.ToString() ?? "",
    };
    private static System.Globalization.CultureInfo Inv => System.Globalization.CultureInfo.InvariantCulture;
    private static System.NotSupportedException Structural()
        => new("value.Reader is scalar-only — a structured value needs a format parser, not this reader.");
}
