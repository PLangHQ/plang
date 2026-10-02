namespace app.type.item.size;

/// <summary>
/// PLang <c>size</c> value — a count of bytes that reads and writes itself. Its kinds are its standards: <c>iec</c>
/// (1024; <c>500 KiB</c>, <c>95.4 MiB</c>) and <c>si</c> (1000; <c>512 kB</c>, <c>100 MB</c>). A size read from text keeps
/// the standard its suffix names; one made from a count is written in the standard its asker's
/// <c>%!app.type.size.setting.standard%</c> names. Its value is the exact bytes; the text rounds.
/// </summary>
[global::app.Attributes.PlangType("size")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.setting.ISetting<setting.@this>, System.IEquatable<@this>
{
    public static string Shape => "string";

    /// <summary>The bytes.</summary>
    public long Value { get; }

    // the standard it is written in (iec, si), born with it; and its type, {size, <kind>}, made once
    private readonly kind.@this _kind;
    private readonly global::app.type.@this _type;

    /// <summary>A count in <paramref name="kind"/>'s standard.</summary>
    public @this(long bytes, kind.@this kind)
    {
        Value = bytes;
        _kind = kind;
        _type = new global::app.type.@this("size", typeof(@this), kind.Name);
    }

    /// <summary>A count, written in the standard its asker's setting names.</summary>
    public @this(long bytes, actor.context.@this context)
        : this(bytes, context.Setting.Of<setting.@this>().Standard.Value) { }

    // a count with no asker: the setting's own default
    private @this(long bytes) : this(bytes, new setting.@this().Standard.Value) { }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;

    /// <summary>Its text in its kind — <c>95.4 MiB</c>, <c>100 MB</c>; <c>%s.text%</c>.</summary>
    public string Text => _kind.Text(Value);

    /// <summary>A size writes its text in its own kind: <c>100 MB</c> stays <c>100 MB</c>.</summary>
    public override void Write(global::app.type.format.IWriter w) => w.Content(Text, _kind);
    protected internal override global::app.type.@this Type => _type;

    /// <summary>THE PURE CORE — a <c>size</c> passes through; a whole number is a count of bytes; a string reads in the
    /// standard its suffix names (<see cref="Resolve"/>); anything else declines (<c>null</c>).</summary>
    public static @this? Create(object? raw) => Read(raw, null);

    // raw as a size: a count written in its asker's standard, or the setting's default with no asker
    private static @this? Read(object? raw, global::app.actor.context.@this? context)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        long? count = value switch
        {
            long l => l,
            int i => i,
            decimal d when d == decimal.Truncate(d) => (long)d,
            double d when d == System.Math.Truncate(d) => (long)d,
            _ => null,
        };
        if (count is { } bytes) return context != null ? new @this(bytes, context) : new @this(bytes);
        return value is string s ? Resolve(s, context) : null;
    }

    /// <summary>The ICreate courier face — the pure core with its asker, so a count is written in the asker's
    /// standard; on decline lands the reason on <paramref name="data"/> (a bad string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (Read(value, data.Context) is { } built)
        {
            // asked for in a standard (`as si`): the same bytes, written in that one
            if (declared?.kind is { IsEmpty: false } asked && !string.Equals(asked.Name, built._kind.Name, System.StringComparison.OrdinalIgnoreCase)
                && data.Context?.App.type.list["size"].kind[asked.Name] is kind.@this standard)
                return new @this(built.Value, standard);
            return built;
        }
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.Error($"Cannot read '{s}' as a size — expected a number and its unit: IEC (500 KiB, 95.4 MiB) or SI (512 kB, 100 MB).", "SizeParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to size.", "SizeConversionFailed", 400));
        return null;
    }

    public static implicit operator long(@this s) => s.Value;

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);

    /// <summary>Its text in its kind (<see cref="Text"/>).</summary>
    public override string ToString() => Text;

    // ---- Truthiness (item): zero is falsy ----
    public override bool IsTruthy() => Value != 0;

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks number and text — a number is a count of bytes, a text reads in its suffix's standard.</summary>
    public override int Rank => 400;

    /// <summary>Byte ordering in caller order; the other side coerces into size through the pure <c>Create</c> core.
    /// Non-coercible → Incomparable.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        var b = other as @this ?? Create(other);
        if (b is null) return new(global::app.data.Comparison.Incomparable);
        var c = Value.CompareTo(b.Value);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }

    // ---- Equality (by bytes: 1 MB and 1000 kB are one size) ----
    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
