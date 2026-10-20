namespace app.type.item.size;

/// <summary>
/// PLang <c>size</c> value — a count of bytes that reads and writes itself. Each standard a size is written in is a
/// kind of it and a class of its own: <c>iec</c> (1024; <c>500 KiB</c>, <c>95.4 MiB</c>) and <c>si</c> (1000;
/// <c>512 kB</c>, <c>100 MB</c>). A size read from text is the standard its suffix names; one made from a count is
/// made by the standard its asker's <c>%!app.type.size.setting.standard%</c> names. Its value is the exact bytes; the
/// text rounds.
/// </summary>
[global::app.Attributes.PlangType("size"), global::app.Attributes.Kinds, global::app.Attributes.Choice]
public abstract partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.setting.ISetting<setting.@this>, System.IEquatable<@this>
{
    public static string Shape => "string";

    /// <summary>The bytes.</summary>
    public long Value { get; }

    /// <summary>A count born holding <paramref name="type"/> — the app's <c>{size, &lt;standard&gt;}</c>.</summary>
    protected @this(long bytes, global::app.type.@this type) : base(type) => Value = bytes;

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;

    /// <summary>Its text in its standard — <c>95.4 MiB</c>, <c>100 MB</c>; <c>%s.text%</c>.</summary>
    public abstract string Text { get; }

    /// <summary>A size writes its text in the standard it is: <c>100 MB</c> stays <c>100 MB</c>.</summary>
    protected internal abstract override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer);

    /// <summary>A count of bytes, made by the standard its asker's setting names.</summary>
    public static @this Create(long bytes, global::app.actor.context.@this context)
        => Of(bytes, context.Setting.Of<setting.@this>().Standard.Value, new global::app.data.@this("", context: context))
           ?? throw new System.InvalidOperationException($"the size standard {context.Setting.Of<setting.@this>().Standard.Value.Name} made no size of {bytes} bytes.");

    // a count made by its standard: the standard, a kind of size, is the type its values are as a kind of the app's size
    private static @this? Of(long bytes, global::app.type.kind.@this<@this> standard, global::app.data.@this data)
        => new global::app.type.@this(standard.Owner!, standard, false, null).Make(bytes, data) as @this;

    // raw as a size: a count made by its asker's standard, a text the standard its suffix names
    private static @this? Read(object? raw, global::app.actor.context.@this context)
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
        if (count is { } bytes) return Create(bytes, context);
        return value is string s ? Resolve(s, context) : null;
    }

    /// <summary>The ICreate courier face — a count is made by the asker's standard; on decline lands the reason on
    /// <paramref name="data"/> (a bad string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (Read(value, data.Context) is { } built)
        {
            // asked for in a standard (`as si`): the same bytes, made by that one
            if (declared?.kind is { IsEmpty: false } asked && !string.Equals(asked.Name, built.Type.kind.Name, System.StringComparison.OrdinalIgnoreCase)
                && data.Context.App.type.list["size"].kind[asked.Name] is global::app.type.kind.@this<@this> standard)
                return Of(built.Value, standard, data);
            return built;
        }
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.@this($"Cannot read '{s}' as a size — expected a number and its unit: IEC (500 KiB, 95.4 MiB) or SI (512 kB, 100 MB).", "SizeParseFailed", 400)
            : new global::app.error.@this($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to size.", "SizeConversionFailed", 400));
        return null;
    }

    /// <summary>The bytes <paramref name="text"/> is in the standard whose units, smallest first, are
    /// <paramref name="units"/>, one of the smallest <paramref name="step"/> bytes — a number and one of its units, its
    /// case ignored; null when it isn't written in it.</summary>
    protected static long? Bytes(string text, long step, string[] units)
    {
        var form = System.Text.RegularExpressions.Regex.Match(text.Trim(), @"^(\d+(?:\.\d+)?)\s*([a-z]+)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!form.Success) return null;
        var unit = System.Array.FindIndex(units, u => string.Equals(u, form.Groups[2].Value, System.StringComparison.OrdinalIgnoreCase));
        if (unit < 0) return null;
        var amount = decimal.Parse(form.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i <= unit; i++) amount *= step;
        return (long)decimal.Round(amount);
    }

    /// <summary><see cref="Value"/> written in the standard whose units are <paramref name="units"/>: its largest unit
    /// that is at least one, with one decimal at most (<c>95.4 MiB</c>, <c>100 MB</c>); under the smallest unit, bytes
    /// (<c>512 B</c>).</summary>
    protected string Form(long step, string[] units)
    {
        decimal amount = Value;
        var unit = -1;
        while (unit + 1 < units.Length && System.Math.Abs(amount) >= step)
        {
            amount /= step;
            unit++;
        }
        var number = decimal.Round(amount, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return $"{number} {(unit < 0 ? "B" : units[unit])}";
    }

    public static implicit operator long(@this s) => s.Value;

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);

    /// <summary>Its text in its standard (<see cref="Text"/>).</summary>
    public override string ToString() => Text;

    // ---- Truthiness (item): zero is falsy ----
    public override bool IsTruthy() => Value != 0;

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks number and text — a number is a count of bytes, a text reads in its suffix's standard.</summary>
    public override int Rank => 400;

    /// <summary>Byte ordering in caller order; the other side coerces into size through <see cref="Read"/>.
    /// Non-coercible → Incomparable.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        var b = other as @this ?? Read(other, context);
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
