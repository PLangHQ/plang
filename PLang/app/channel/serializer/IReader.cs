namespace app.channel.serializer;

/// <summary>
/// The kind of the value token an <see cref="IReader"/> is positioned at —
/// the read-side branch signal (a write is told its shape; a read must look).
/// Maps the self-describing token vocabulary every format shares; a format with
/// no per-token type (CSV cells are all strings) answers <see cref="String"/>
/// uniformly and lets the type coerce.
/// </summary>
public enum TokenKind
{
    Null,
    Bool,
    Number,
    String,
    Array,
    Object,
}

/// <summary>
/// Format-agnostic <em>pull</em> surface — the symmetric mirror of
/// <see cref="IWriter"/>. Where a type <em>pushes</em> its value into an
/// <see cref="IWriter"/> (<c>writer.Long(42)</c>), it <em>pulls</em> its value
/// from an <see cref="IReader"/> (<c>reader.Long()</c>). JSON
/// (<see cref="json.Reader"/>) is the first impl; CSV, protobuf, a raw-bytes
/// reader ship later as siblings without touching a single type.
///
/// <para>The read is one <em>synchronous</em> forward pass: the reader is a stack
/// local threaded by <c>ref</c> into each type's read, never stored in a field and
/// never boxed (a <see cref="json.Reader"/> is a <c>ref struct</c> over a
/// <c>Utf8JsonReader</c>). A type's read is generic over the reader
/// (<c>where TReader : IReader, allows ref struct</c>), monomorphized per format
/// at the call site — zero boxing, zero storage.</para>
///
/// <para><b>Cursor contract.</b> Every leaf and structure read leaves the reader's
/// current token at the value's <em>last</em> token (a scalar stays on its single
/// token; an array/object ends on its <c>End</c> token). The drivers
/// (<see cref="NextElement"/> / <see cref="NextName"/>) advance one token to reach
/// the next member. This mirrors the natural forward-only model and lets a value
/// read compose: after a type reads its value, the parent calls one advance.</para>
/// </summary>
public interface IReader
{
    /// <summary>
    /// Short format token — <c>"json"</c>, <c>"csv"</c>, <c>"protobuf"</c>, …
    /// The read-side mirror of <see cref="IWriter.Format"/>. A type's read
    /// branches on this when it decodes differently per format.
    /// </summary>
    string Format { get; }

    /// <summary>The kind of the value token the reader is currently positioned at — branch on it.</summary>
    TokenKind Peek();

    // Leaf pulls — read the current token as this CLR shape. The cursor stays on
    // the token (its value's last token). Mirror of IWriter's leaf pushes.
    bool Null();                          // true if the current token is the null literal
    bool Bool();
    int Int();
    long Long();
    float Float();
    double Double();
    decimal Decimal();
    string String();
    byte[] Bytes();
    System.DateTime DateTime();
    System.DateTimeOffset DateTimeOffset();
    System.TimeSpan TimeSpan();
    System.Guid Guid();

    // Structure. Precondition for Begin*: the cursor is at the Start{Array,Object}
    // token. while (NextElement()) { read one element } EndArray();
    void BeginArray();

    /// <summary>
    /// Advance to the next array element. Returns <c>false</c> at the end (cursor
    /// positioned on <c>EndArray</c>); otherwise <c>true</c> with the cursor on the
    /// element's first token.
    /// </summary>
    bool NextElement();
    void EndArray();

    void BeginObject();

    /// <summary>
    /// Advance to the next object member. Returns <c>false</c> at the end (cursor on
    /// <c>EndObject</c>); otherwise <c>true</c> with <paramref name="name"/> set and
    /// the cursor on the member value's first token.
    /// </summary>
    bool NextName(out string name);
    void EndObject();

    /// <summary>
    /// Capture the current value's encoded bytes verbatim, without decoding —
    /// the lazy / never-narrowed passthrough. Advances the cursor past the value
    /// (to its last token), like <see cref="Skip"/>. The bytes are this format's
    /// own encoding of the value (JSON text for JSON), fed straight into an
    /// <c>item.source</c> for later content decode through the matching reader.
    /// </summary>
    byte[] RawValue();

    /// <summary>Skip the current value entirely; advances the cursor past it (to its last token).</summary>
    void Skip();
}
