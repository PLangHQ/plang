namespace app.type.item.archive;

/// <summary>
/// PLang <c>archive</c> value — a compression layer over a serialized envelope.
/// Backed by the compressed <c>byte[]</c> plus the algorithm that produced it
/// (<c>gzip</c> today, carried as <see cref="Algo"/>). The decompressed bytes are
/// themselves a serialized envelope — <c>data</c> is the lowest layer.
///
/// <para>The self-describing wire form <c>{@schema:"archive", type:"gzip",
/// value:"&lt;base64&gt;"}</c> — where the reader dispatches on <c>@schema</c> to
/// pick the decompressor — is forthcoming. Today an <c>archive</c> rides as a
/// bare-bytes leaf carried on a <c>Data</c>, and <c>Decompress</c> keys off the
/// item type rather than a string label. Replacing the prior <c>clr</c>-labeled
/// byte[] courier: a real item is not reflected as a transparent property bag, so
/// it never drags the runtime context graph onto the wire.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "(archive)";
    public static string Shape => "string";

    /// <summary>The compressed bytes — the layer's payload.</summary>
    public byte[] Value { get; }

    /// <summary>The compression algorithm — <c>gzip</c> today; the layer's wire
    /// <c>type</c>. An ordinary typed property stamped at creation.</summary>
    public string Algo { get; }

    public @this(byte[] value, string algo = "gzip")
    {
        Value = value ?? System.Array.Empty<byte>();
        Algo = string.IsNullOrEmpty(algo) ? "gzip" : algo;
    }

    protected internal override global::app.type.@this Type
        => new("archive", typeof(byte[])) { Kind = Algo is { } k ? new global::app.type.kind.@this(k) : null };

    /// <summary>The CLR exit door — archive hands its own compressed bytes.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Bytes(Value);

    /// <summary>Non-empty bytes are truthy.</summary>
    public override bool IsTruthy() => Value.Length > 0;

    /// <summary>Bare base64 — the serializer renders this.</summary>
    public override string ToString() => System.Convert.ToBase64String(Value);
}
