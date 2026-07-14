namespace app.type.item.binary;

/// <summary>
/// PLang <c>binary</c> value — raw bytes as a first-class value (file/HTTP byte
/// reads, crypto output). Sibling to <c>image</c> but untyped (no MIME). Backed by
/// a CLR <c>byte[]</c>; the bare wire form is base64.
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "(bytes)";
    public static string Shape => "string";

    public byte[] Value { get; }

    /// <summary>The value's kind — the byte-format vocabulary. An ordinary
    /// typed property stamped at creation, never after.</summary>
    public string? Kind { get; init; }

    protected internal override global::app.type.@this Type
        => new("binary", typeof(byte[])) { Kind = Kind is { } k ? new global::app.type.kind.@this(k) : null };

    public @this(byte[] value) { Value = value ?? System.Array.Empty<byte>(); }

    /// <summary>THE PURE CORE — a <c>binary</c> passes through; a raw <c>byte[]</c> passes; a base64
    /// string decodes; anything else (or non-base64) declines (<c>null</c>). Shared by the ICreate
    /// courier and comparison coercion.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        switch (value)
        {
            case byte[] b: return (@this)b;
            case string s:
                try { return (@this)System.Convert.FromBase64String(s); }
                catch (System.FormatException) { return null; }
            default: return null;
        }
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a non-base64 string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        object? clr = (value as global::app.type.item.@this)?.Clr<object>() ?? value;
        data.Fail(clr is string
            ? new global::app.error.Error("Cannot parse string as binary — expected base64.", "BinaryParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {(value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name} to binary.", "BinaryConversionFailed", 400));
        return null;
    }

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

    /// <summary>binary's byte face IS its bytes.</summary>
    public override byte[]? RawBytes => Value;

    /// <summary>Non-empty bytes are truthy.</summary>
    public override bool IsTruthy() => Value.Length > 0;

    /// <summary>Bare base64 — the serializer renders this.</summary>
    public override string ToString() => System.Convert.ToBase64String(Value);

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks text — bytes never compare lexically.</summary>
    public override int Rank => 250;

    /// <summary>Equality-only: same byte sequence → <c>Equal</c>, else <c>NotEqual</c>;
    /// a side that can't become bytes → <c>Incomparable</c>. No order.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
    {
        var b = other as @this ?? Create(other);
        return new(b is null ? global::app.data.Comparison.Incomparable
                 : Value.AsSpan().SequenceEqual(b.Value) ? global::app.data.Comparison.Equal
                 : global::app.data.Comparison.NotEqual);
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
