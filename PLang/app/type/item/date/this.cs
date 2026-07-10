namespace app.type.item.date;

/// <summary>
/// PLang <c>date</c> value — its own type, backed by <see cref="System.DateOnly"/>.
/// <b>Distinct from <c>datetime</c></b>: the historical collapse (ScalarComparer
/// coerced <c>DateOnly → DateTimeOffset</c> and classed it <c>datetime</c>) ends
/// with this wrapper. Order/equality are day-precision; the bare wire form is
/// ISO <c>yyyy-MM-dd</c>.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "2024-03-15";
    public static string Shape => "string";

    public System.DateOnly Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(ToString());
    protected internal override global::app.type.@this Mint() => new("date", typeof(System.DateOnly));

    public @this(System.DateOnly value) { Value = value; }

    /// <summary>THE PURE CORE — a <c>date</c> passes through; a DateOnly/DateTime/DateTimeOffset or
    /// an ISO <c>yyyy-MM-dd</c> string parses; anything else declines (<c>null</c>). Shared by the
    /// ICreate courier and comparison coercion.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value switch
        {
            System.DateOnly d0 => new @this(d0),
            System.DateTime dt => new @this(System.DateOnly.FromDateTime(dt)),
            System.DateTimeOffset dto => new @this(System.DateOnly.FromDateTime(dto.DateTime)),
            string s when System.DateOnly.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var d) => new @this(d),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a bad ISO string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.Error($"Cannot parse '{s}' as date — expected ISO yyyy-MM-dd.", "DateParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {((value as global::app.type.item.@this)?.Mint().Name ?? value?.GetType().Name)} to date.", "DateConversionFailed", 400));
        return null;
    }

    public int Year => Value.Year;
    public int Month => Value.Month;
    public int Day => Value.Day;

    /// <summary>Bare ISO date form (<c>yyyy-MM-dd</c>) — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Date family outranks text — ISO text coerces into the date, not vice versa.</summary>
    public override int Rank => 500;

    /// <summary>Ordered comparison in caller order; the other side coerces into date through
    /// the pure <c>Create</c> core (ISO text → date). Non-coercible → Incomparable.</summary>
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
        @this d => Value == d.Value,
        System.DateOnly d => Value == d,
        _ => false,
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
