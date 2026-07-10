namespace app.type.item.time;

/// <summary>
/// PLang <c>time</c> value — its own type, backed by <see cref="System.TimeOnly"/>.
/// Today <c>ScalarComparer</c> has no <c>TimeOnly</c> arm at all (time is
/// unhandled); this wrapper closes that gap. Order/equality are within
/// time-of-day; the bare wire form is ISO <c>HH:mm:ss[.fffffff]</c>.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "10:30:00";
    public static string Shape => "string";

    public System.TimeOnly Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(ToString());
    protected internal override global::app.type.@this Mint() => new("time", typeof(System.TimeOnly));

    public @this(System.TimeOnly value) { Value = value; }

    /// <summary>THE PURE CORE — a <c>time</c> passes through; a TimeOnly/DateTime/DateTimeOffset or
    /// an ISO <c>HH:mm:ss</c> string parses; anything else declines (<c>null</c>). Shared by the
    /// ICreate courier and comparison coercion.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value switch
        {
            System.TimeOnly t0 => new @this(t0),
            System.DateTime dt => new @this(System.TimeOnly.FromDateTime(dt)),
            System.DateTimeOffset dto => new @this(System.TimeOnly.FromDateTime(dto.DateTime)),
            string s when System.TimeOnly.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var t) => new @this(t),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a bad ISO string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.Error($"Cannot parse '{s}' as time — expected ISO HH:mm:ss.", "TimeParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {((value as global::app.type.item.@this)?.Mint().Name ?? value?.GetType().Name)} to time.", "TimeConversionFailed", 400));
        return null;
    }

    public int Hour => Value.Hour;
    public int Minute => Value.Minute;
    public int Second => Value.Second;

    /// <summary>Bare ISO time form — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture);

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Date family outranks text — ISO text coerces into the time.</summary>
    public override int Rank => 450;

    /// <summary>Ordered comparison in caller order; the other side coerces into time through
    /// the pure <c>Create</c> core (ISO text → time). Non-coercible → Incomparable.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other)
    {
        var b = other as @this ?? Create(other);
        if (b is null) return new(global::app.data.Comparison.Incomparable);
        var c = Value.CompareTo(b.Value);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }

    public bool AreEqual(object? other) => other switch
    {
        @this t => Value == t.Value,
        System.TimeOnly t => Value == t,
        _ => false,
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
