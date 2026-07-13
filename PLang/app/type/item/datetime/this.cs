namespace app.type.item.datetime;

/// <summary>
/// PLang <c>datetime</c> value — backed by <see cref="System.DateTimeOffset"/>
/// (the wire is tz-aware end to end). Accepts a CLR <see cref="System.DateTime"/>
/// on construction (the type map aliases <c>DateTime → datetime</c>).
///
/// <para>Behavior (compare, equality, parts) lives on the wrapper as a
/// <c>: item.@this</c>, so the <c>is DateTimeOffset</c> consumer-switches
/// collapse into method calls. Order is chronological by instant; equality is
/// same-instant; the bare wire form is ISO round-trip (<c>"o"</c>).</para>
/// </summary>
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "2024-03-15T10:30:00+00:00";
    public static string Shape => "string";

    public System.DateTimeOffset Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.DateTimeOffset(Value);
    protected internal override global::app.type.@this Type => new("datetime", typeof(System.DateTimeOffset));

    public @this(System.DateTimeOffset value) { Value = value; }

    /// <summary>THE PURE CORE — a <c>datetime</c> passes through; a DateTimeOffset/DateTime or an
    /// ISO-8601 string parses (via <see cref="Resolve"/>, whose context is unused); anything else
    /// declines (<c>null</c>). Shared by the ICreate courier and comparison coercion.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value switch
        {
            System.DateTimeOffset dto => new @this(dto),
            System.DateTime dt => new @this(new System.DateTimeOffset(dt)),
            string s => Resolve(s, null!),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a bad ISO string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.Error($"Cannot parse '{s}' as datetime — expected ISO-8601 (e.g. 2024-03-15T10:30:00+00:00).", "DateTimeParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to datetime.", "DateTimeConversionFailed", 400));
        return null;
    }

    /// <summary>Accepts a CLR <see cref="System.DateTime"/> — stored as an offset.</summary>
    public @this(System.DateTime value) { Value = new System.DateTimeOffset(value); }

    // ---- Parts (behavioral targets of the is-DateTimeOffset sweep) ----
    public int Year => Value.Year;
    public int Month => Value.Month;
    public int Day => Value.Day;
    public int Hour => Value.Hour;
    public int Minute => Value.Minute;
    public int Second => Value.Second;
    public int Millisecond => Value.Millisecond;
    public long Ticks => Value.Ticks;
    public int DayOfYear => Value.DayOfYear;
    public System.DayOfWeek DayOfWeek => Value.DayOfWeek;

    // Compound parts carry their own plang type: the calendar day is a `date`, the
    // wall-clock time a `time`, the zone offset a `duration`.
    public global::app.type.item.date.@this Date => new(System.DateOnly.FromDateTime(Value.Date));
    public global::app.type.item.time.@this TimeOfDay => new(System.TimeOnly.FromTimeSpan(Value.TimeOfDay));
    public global::app.type.item.duration.@this Offset => new(Value.Offset);

    /// <summary>Bare ISO round-trip form — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Date family outranks text — ISO text coerces into the datetime.</summary>
    public override int Rank => 550;

    /// <summary>Instant ordering in caller order; the other side coerces into datetime through
    /// the pure <c>Create</c> core (ISO text → datetime). Non-coercible → Incomparable.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
    {
        var b = other as @this ?? Create(other);
        if (b is null) return new(global::app.data.Comparison.Incomparable);
        var c = Value.ToUniversalTime().CompareTo(b.Value.ToUniversalTime());
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }

    // ---- Equality + order (by instant) ----
    public bool AreEqual(object? other) => other switch
    {
        @this d => Value.ToUniversalTime() == d.Value.ToUniversalTime(),
        System.DateTimeOffset dto => Value.ToUniversalTime() == dto.ToUniversalTime(),
        System.DateTime dt => Value.ToUniversalTime() == new System.DateTimeOffset(dt).ToUniversalTime(),
        _ => false,
    };

    public bool Equals(@this? other) => other is not null && AreEqual(other);
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.ToUniversalTime().GetHashCode();
}
