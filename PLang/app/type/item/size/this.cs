namespace app.type.item.size;

/// <summary>
/// PLang <c>size</c> value — a count of bytes that reads and writes itself. Each standard a size is written in is a
/// kind of it and a class of its own: <c>iec</c> (1024; <c>500 KiB</c>, <c>95.4 MiB</c>) and <c>si</c> (1000;
/// <c>512 kB</c>, <c>100 MB</c>). A size read from text is the standard its suffix names; one made from a count is
/// made by the standard its asker's <c>%!app.type.size.setting.standard%</c> names. Its value is the exact bytes; the
/// text rounds.
/// </summary>
[global::app.Attributes.PlangType("size"), global::app.Attributes.Kinds, global::app.Attributes.Choice]
public partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
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

    /// <summary>Its text in its standard — <c>95.4 MiB</c>, <c>100 MB</c>; <c>%s.text%</c>. One born with no standard (a
    /// file's length, a download's bytes) is written in the one <paramref name="context"/>'s setting names, read once.</summary>
    public virtual async System.Threading.Tasks.ValueTask<string> Text(global::app.actor.context.@this context)
    {
        var standard = (await context.Setting.Get<setting.@this>()).Standard.Value;
        var written = Of(Value, standard, new global::app.data.@this("", context: context))
            ?? throw new System.InvalidOperationException($"the size standard {standard.Name} made no size of {Value} bytes.");
        return await written.Text(context);
    }

    /// <summary>A size writes its text in the standard it is: <c>100 MB</c> stays <c>100 MB</c>; one with no standard,
    /// in its asker's.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
        => writer.String(await Text(writer.Context));

    /// <summary>A count of bytes with no standard of its own, written in the one its asker's setting names.</summary>
    public static @this Create(long bytes, global::app.actor.context.@this context)
        => new(bytes, context.App.type.list["size"]);

    // a count made by its standard, through the app's entry for {size, <standard>}
    private static @this? Of(long bytes, global::app.type.kind.@this<@this> standard, global::app.data.@this data)
        => data.Context.App.type.list[new global::app.type.@this("size", standard.Name), data.Context].Make(bytes, data) as @this;

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
    public static @this? Create(object? value, global::app.type.@this? type, global::app.data.@this data)
    {
        // asked for in a standard (`as si`): that standard makes it, from a count or a text in its own form only
        if (type?.kind is { IsEmpty: false } asked && data.Context.App.type.list["size"].kind[asked.Name] is global::app.type.kind.@this<@this> standard)
            return data.Context.App.type.list[new global::app.type.@this("size", standard.Name), data.Context].Make(value, data) as @this;
        if (Read(value, data.Context) is { } built) return built;
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.@this($"Cannot read '{s}' as a size — expected a number and its unit: IEC (500 KiB, 95.4 MiB) or SI (512 kB, 100 MB).", "SizeParseFailed", 400)
            : new global::app.error.@this($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to size.", "SizeConversionFailed", 400));
        return null;
    }

    /// <summary>The bytes <paramref name="raw"/> is in the standard whose units, smallest first, are
    /// <paramref name="units"/>, one of the smallest <paramref name="step"/> bytes: a count is itself; a text a number
    /// and one of its units, its case ignored; null when it is neither.</summary>
    protected static long? Count(object? raw, long step, string[] units)
        => (raw is global::app.type.item.@this leaf ? leaf.Clr<object>() : raw) switch
        {
            long bytes => bytes,
            int bytes => bytes,
            string text => Count(text, step, units),
            _ => null,
        };

    /// <summary>A standard refuses a text in another form, naming itself and how it is written
    /// (<c>'100 MiB' is not a size in si (si is written 100 MB)</c>); anything else declines unnamed.</summary>
    protected static void Refuse(object? raw, string standard, string written, global::app.data.@this data)
    {
        if ((raw is global::app.type.item.@this leaf ? leaf.Clr<object>() : raw) is string text)
            data.Fail(new global::app.error.@this($"'{text}' is not a size in {standard} ({standard} is written {written})", "SizeParseFailed", 400));
    }

    // the bytes a text in that standard is
    private static long? Count(string text, long step, string[] units)
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

    // its value written in the standard whose units are these: its largest unit that is at least one, with one decimal
    // at most (95.4 MiB, 100 MB); under the smallest unit, bytes (512 B)
    protected string Spell(long step, string[] units)
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

    /// <summary>Its bytes — a standard's size says its own text (<see cref="Text"/>).</summary>
    public override string ToString() => $"{Value} B";

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
