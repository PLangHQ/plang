namespace app.type.time;

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

    public int Hour => Value.Hour;
    public int Minute => Value.Minute;
    public int Second => Value.Second;

    /// <summary>Bare ISO time form — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture);

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Date family outranks text — ISO text coerces into the time.</summary>
    internal static int CompareRank => 45;

    /// <summary>Ordered comparison in caller order; the other side coerces through this
    /// family's own Convert hook (ISO text → time). Non-coercible → Incomparable.</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        var ca = CoerceOwn(a);
        var cb = CoerceOwn(b);
        if (ca == null || cb == null) return global::app.data.Comparison.Incomparable;
        var c = ca.Value.CompareTo(cb.Value);
        return c < 0 ? global::app.data.Comparison.Less
             : c > 0 ? global::app.data.Comparison.Greater
             : global::app.data.Comparison.Equal;
    }

    private static @this? CoerceOwn(object? v) => v as @this
        ?? convert.@this.OfStatic(typeof(@this),
               global::app.type.item.@this.Backing(v), null, null)?.Peek() as @this;

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
