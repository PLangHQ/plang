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
public sealed partial class @this : global::app.type.item.@this,
    global::app.data.IEquatableValue, global::app.data.IOrderableValue,
    System.IEquatable<@this>
{
    public static string Example => "2024-03-15T10:30:00+00:00";
    public static string Shape => "string";

    public System.DateTimeOffset Value { get; }
    public override object? ToRaw() => Value;

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

    // ---- Equality + order (by instant) ----
    public bool AreEqual(object? other) => other switch
    {
        @this d => Value.ToUniversalTime() == d.Value.ToUniversalTime(),
        System.DateTimeOffset dto => Value.ToUniversalTime() == dto.ToUniversalTime(),
        System.DateTime dt => Value.ToUniversalTime() == new System.DateTimeOffset(dt).ToUniversalTime(),
        _ => false,
    };

    public int Order(object? other) => other switch
    {
        @this d => Value.CompareTo(d.Value),
        System.DateTimeOffset dto => Value.CompareTo(dto),
        System.DateTime dt => Value.CompareTo(new System.DateTimeOffset(dt)),
        _ => throw new global::app.data.Compare.NotOrderableException(
            $"cannot order datetime against {other?.GetType().Name ?? "null"}"),
    };

    public bool Equals(@this? other) => other is not null && AreEqual(other);
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.ToUniversalTime().GetHashCode();
}
