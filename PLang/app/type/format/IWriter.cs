namespace app.type.format;

/// <summary>
/// Format-encoder protocol: what a value writes itself through.
///
/// <para>Each value writes itself through its own <c>Output</c>, calling the
/// writer's primitives, arrays and records; a C# primitive (or a byte[]) goes
/// through <see cref="Value"/>. The writer decides how each
/// looks in its target format. JSON (<see cref="json.Writer"/>) is the first impl;
/// protobuf / MsgPack / CBOR ship later as siblings without touching any
/// domain type.</para>
///
/// <para>The surface is deliberately minimal: leaf primitives, an array
/// bracket (count optional — JSON ignores, protobuf may use), and a Data
/// record (<see cref="BeginRecord"/>): every Data tells the writer what it is, and
/// the writer decides how much of it to show.</para>
/// </summary>
public interface IWriter
{
    /// <summary>
    /// The kind this writer writes — its output kind (item's <c>json</c>, wire's <c>plang</c>, text's own format,
    /// list's <c>formal</c>): the app's own kind object, never a name. What a value looks like in it is the kind's
    /// to say, not the value's.
    /// </summary>
    global::app.type.kind.@this Kind { get; }

    /// <summary>What this write shows — chosen where the write starts (a <c>.pr</c> is Store, a response Out, a dump
    /// Debug), held for the whole write. A value asks it nothing directly: its members show by their tags, and a value
    /// that holds variables is kept or used by it.</summary>
    global::app.View View { get; }

    /// <summary>Who asks for this write — the asker every value written reads with (its rows, its renders). Never
    /// null: a write no one asks for is the app's system actor's.</summary>
    global::app.actor.context.@this Context { get; }

    /// <summary><paramref name="value"/> written into this writer: the writer hands it back to the value's own write for
    /// the writer's own class. A writer whose format some value looks different in (text, formal) answers with its
    /// typed self; any other writer gets the value's own write.</summary>
    System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value) => value.Write(this);

    void Null();
    void Bool(bool value);
    void Int(int value);
    void Long(long value);
    void Float(float value);
    void Double(double value);
    void String(string value);
    void DateTime(System.DateTime value);
    void DateTimeOffset(System.DateTimeOffset value);
    void TimeSpan(System.TimeSpan value);
    void Guid(System.Guid value);
    void Enum(System.Enum value);
    void Decimal(decimal value);

    /// <summary>
    /// Bytes of <paramref name="kind"/> — what they are (an image's format, json content, a text, plain bytes). The
    /// writer decides how they look: a writer the kind owns takes them as a token of its own format, as they are;
    /// any other writes them its own way (json base64 or the text they spell, text their size or their text).
    /// </summary>
    void Bytes(byte[] value, global::app.type.kind.@this kind);

    /// <summary>
    /// Emit content already valid in the target format, verbatim — a source value's
    /// undecoded raw form, passed through without re-encoding. JSON emits it unquoted
    /// (<c>WriteRawValue</c>) so a raw json object/number rides inline; text writes the
    /// string as-is. The caller (a <c>source</c> leaf) decides when its raw is format-ready.
    /// Default falls back to a quoted string for writers that have no verbatim concept.
    /// </summary>
    void Raw(string value) => String(value);

    /// <summary>Characters nobody should see (a key, a password). Shown, they are <c>****</c>; a writer of plang's own
    /// store keeps them whole.</summary>
    void Sensitive(string characters) => String("****");

    /// <summary>
    /// Characters that are content of a format — a text of kind <paramref name="kind"/> (json, md, …). A writer the
    /// kind owns takes them as a token of its own format, as they are; any other writes them as text.
    /// </summary>
    void Content(string value, global::app.type.kind.@this kind)
    {
        if (kind.Owns(this)) Raw(value);
        else String(value);
    }

    /// <summary>A variable, named as itself — <paramref name="name"/> without its % signs. Default: the
    /// text <c>%name%</c> as a string; a writer with a bare variable form (formal) writes that.</summary>
    void Variable(string name) => String($"%{name}%");

    /// <summary>A value the step gives that is not named here — a slot still to fill (a permission the writer fills
    /// from the step's words). Default: the word <c>given</c>; a writer where an unfilled slot is its bare name
    /// (formal) writes nothing.</summary>
    void Given() => String("given");

    /// <summary>
    /// Begin an array bracket. <paramref name="count"/> is -1 when the writer
    /// cannot determine the length up front; format encoders that need a
    /// known length (protobuf packed repeated, length-prefixed binary
    /// formats) may have to buffer in that case.
    /// </summary>
    void BeginArray(int count);
    void EndArray();

    /// <summary>
    /// Begin a free-form object bracket — the surface a <c>@schema</c> layer
    /// (<c>signature</c> / <c>archive</c> / <c>encryption</c>) uses to render its
    /// own <c>{@schema, …fields…, value:&lt;inner&gt;}</c> shape. The layer owns
    /// the layout: between <see cref="BeginObject"/> and <see cref="EndObject"/>
    /// it calls <see cref="Name"/> then a leaf method (or <see cref="Value"/> for
    /// the nested <c>value</c> slot) per field. Distinct from
    /// <see cref="BeginRecord"/>, a Data telling the writer what it is.
    /// </summary>
    void BeginObject();

    /// <summary>Write a property name inside the current <see cref="BeginObject"/> bracket.</summary>
    void Name(string name);

    void EndObject();

    /// <summary>
    /// A Data begins: it tells the writer what it is (its name, type, properties), its value written next through
    /// the door. The writer decides how much of it to show — plang's writer its record (<c>@schema</c> when it is a
    /// <paramref name="layer"/>, the name in the Store view, the type); a bare format nothing (only the value).
    /// </summary>
    System.Threading.Tasks.ValueTask BeginRecord(app.data.@this record, bool layer) => default;

    /// <summary>The Data's value is written: what the writer shows after it (plang's writer the stored variables and
    /// the properties), and the record closes. A bare format writes nothing.</summary>
    System.Threading.Tasks.ValueTask EndRecord(app.data.@this record) => default;

    /// <summary>
    /// Write a C# primitive (or a byte[], as plain bytes) in the current slot; a plang
    /// value, a Data and a collection each write themselves.
    /// </summary>
    void Value(object? normalized);
}
