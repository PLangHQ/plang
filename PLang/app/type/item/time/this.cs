namespace app.type.item.time;

/// <summary>
/// PLang <c>time</c> value — its own type, backed by <see cref="System.TimeOnly"/>.
/// Its own Order compares and equates within the time of day; the bare wire form
/// is ISO <c>HH:mm:ss[.fffffff]</c>.
/// </summary>
[global::app.Attributes.PlangType("time")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Shape => "string";

    public System.TimeOnly Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter w) { w.String(ToString()); return default; }

    /// <summary>Born holding the app's <c>time</c> type its maker hands it.</summary>
    public @this(System.TimeOnly value, global::app.type.@this type) : base(type) { Value = value; }

    /// <summary>THE PURE CORE — a <c>time</c> passes through; a TimeOnly/DateTime/DateTimeOffset or
    /// an ISO <c>HH:mm:ss</c> string parses into a time of <paramref name="type"/>; anything else declines
    /// (<c>null</c>). Shared by the ICreate courier and comparison coercion.</summary>
    public static @this? Create(object? raw, global::app.type.@this type)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value switch
        {
            System.TimeOnly t0 => new @this(t0, type),
            System.DateTime dt => new @this(System.TimeOnly.FromDateTime(dt), type),
            System.DateTimeOffset dto => new @this(System.TimeOnly.FromDateTime(dto.DateTime), type),
            string s when System.TimeOnly.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var t) => new @this(t, type),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a bad ISO string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (value is @this self) return self;
        // made by the type that asks; with none (a read that names no app) no time is made
        if (declared is { } type && Create(value, type) is { } built) return built;
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.@this($"Cannot parse '{s}' as time — expected ISO HH:mm:ss.", "TimeParseFailed", 400)
            : new global::app.error.@this($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to time.", "TimeConversionFailed", 400));
        return null;
    }

    [global::app.LlmBuilder]
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
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        var b = other as @this ?? Create(other, Type);
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
