namespace app.type.datetime;

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
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "2024-03-15T10:30:00+00:00";
    public static string Shape => "string";

    public System.DateTimeOffset Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    internal override object? ToRaw() => Value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.DateTimeOffset(Value);
    protected internal override global::app.type.@this Mint() => new("datetime", typeof(System.DateTimeOffset));

    public @this(System.DateTimeOffset value) { Value = value; }
    /// <summary>Accepts a CLR <see cref="System.DateTime"/> — stored as an offset.</summary>
    public @this(System.DateTime value) { Value = new System.DateTimeOffset(value); }

    // ---- Parts (behavioral targets of the is-DateTimeOffset sweep) ----
    public int Year => Value.Year;
    public int Month => Value.Month;
    public int Day => Value.Day;
    public int Hour => Value.Hour;
    public int Minute => Value.Minute;
    public int Second => Value.Second;

    /// <summary>Bare ISO round-trip form — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Date family outranks text — ISO text coerces into the datetime.</summary>
    internal static int CompareRank => 55;

    /// <summary>Instant ordering in caller order; the other side coerces through this
    /// family's own Convert hook (ISO text → datetime). Non-coercible → Incomparable.</summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        var ca = CoerceOwn(a);
        var cb = CoerceOwn(b);
        if (ca == null || cb == null) return global::app.data.Comparison.Incomparable;
        var c = ca.Value.ToUniversalTime().CompareTo(cb.Value.ToUniversalTime());
        return c < 0 ? global::app.data.Comparison.Less
             : c > 0 ? global::app.data.Comparison.Greater
             : global::app.data.Comparison.Equal;
    }

    private static @this? CoerceOwn(object? v) => v as @this
        ?? convert.@this.OfStatic(typeof(@this),
               global::app.type.item.@this.Backing(v), null, null)?.Peek() as @this;

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
