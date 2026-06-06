namespace app.type.binary;

/// <summary>
/// PLang <c>binary</c> value — raw bytes as a first-class value (file/HTTP byte
/// reads, crypto output). Sibling to <c>image</c> but untyped (no MIME). Backed by
/// a CLR <c>byte[]</c>; the bare wire form is base64.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this,
    global::app.data.IEquatableValue
{
    public static string Example => "(bytes)";
    public static string Shape => "string";

    public byte[] Value { get; }

    public @this(byte[] value) { Value = value ?? System.Array.Empty<byte>(); }

    // Null-tolerant to-byte[] (an absent binary-typed Data has a null wrapper); from-byte[]
    // so `.Ok(bytes)` constructs. byte[] is a reference type, so only @this==@this is
    // defined (a byte[] overload would make `binary == null` ambiguous).
    public static implicit operator byte[]?(@this? b) => b?.Value;
    public static implicit operator @this(byte[] v) => new(v);

    public override object? ToRaw() => Value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Bytes(Value);

    /// <summary>Non-empty bytes are truthy.</summary>
    public override bool IsTruthy() => Value.Length > 0;

    /// <summary>Bare base64 — the serializer renders this.</summary>
    public override string ToString() => System.Convert.ToBase64String(Value);

    public bool AreEqual(object? other) => other switch
    {
        @this b => Value.AsSpan().SequenceEqual(b.Value),
        byte[] arr => Value.AsSpan().SequenceEqual(arr),
        _ => false,
    };

    public override bool Equals(object? obj) => AreEqual(obj);
    public override int GetHashCode()
    {
        var hash = new System.HashCode();
        hash.AddBytes(Value);
        return hash.ToHashCode();
    }
}
