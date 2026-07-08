namespace app.type.binary;

/// <summary>
/// PLang <c>binary</c> value — raw bytes as a first-class value (file/HTTP byte
/// reads, crypto output). Sibling to <c>image</c> but untyped (no MIME). Backed by
/// a CLR <c>byte[]</c>; the bare wire form is base64.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "(bytes)";
    public static string Shape => "string";

    public byte[] Value { get; }

    /// <summary>The value's kind — the byte-format vocabulary. An ordinary
    /// typed property stamped at creation, never after.</summary>
    public string? Kind { get; init; }

    protected internal override global::app.type.@this Mint()
        => new("binary", typeof(byte[])) { Kind = global::app.type.kind.@this.Of(Kind) };

    public @this(byte[] value) { Value = value ?? System.Array.Empty<byte>(); }

    /// <summary>A re-kinded copy — same bytes, the declared kind stamped.</summary>
    public override global::app.type.item.@this Kinded(string? kind) => new @this(Value) { Kind = kind };

    // INBOUND only — the entry lift (`.Ok(bytes)` constructs). The outbound
    // implicit (binary → byte[]) is gone: every site was a silent CLR exit;
    // a reader names the bytes face (`.Value`) at a real .NET edge. byte[] is
    // a reference type, so only @this==@this is defined (a byte[] overload
    // would make `binary == null` ambiguous).
    public static implicit operator @this(byte[] v) => new(v);

    /// <summary>The CLR exit door — binary hands its own bytes.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);

    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.Bytes(Value);

    /// <summary>Non-empty bytes are truthy.</summary>
    public override bool IsTruthy() => Value.Length > 0;

    /// <summary>Bare base64 — the serializer renders this.</summary>
    public override string ToString() => System.Convert.ToBase64String(Value);

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Outranks text — bytes never compare lexically.</summary>
    internal static int CompareRank => 25;

    /// <summary>Equality-only: same byte sequence → <c>Equal</c>, else <c>NotEqual</c>;
    /// a side that isn't bytes → <c>Incomparable</c>. No order.</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        var ba = a as @this ?? (a is byte[] ra ? new @this(ra) : null);
        var bb = b as @this ?? (b is byte[] rb ? new @this(rb) : null);
        if (ba == null || bb == null) return global::app.data.Comparison.Incomparable;
        return ba.Value.AsSpan().SequenceEqual(bb.Value)
            ? global::app.data.Comparison.Equal
            : global::app.data.Comparison.NotEqual;
    }

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
