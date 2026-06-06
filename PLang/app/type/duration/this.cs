namespace app.type.duration;

/// <summary>
/// PLang <c>duration</c> value — backed by <see cref="System.TimeSpan"/> (the CLR
/// type the <c>duration</c> name resolves to; <c>timespan</c> survives as a
/// deprecated alias). PLang devs write prose ("a duration of 5 minutes") and
/// pick types that read like prose.
///
/// <para>Two text forms parse: <c>"1.02:03:04"</c> (TimeSpan canonical) and
/// ISO-8601 duration (<c>"PT5M"</c>, <c>"P1DT2H"</c>).</para>
///
/// <para>Behavior (compare, equality, parts, truthiness) lives on the wrapper
/// as a <c>: item.@this</c>. Order/equality are by span length. <b>Truthiness
/// policy: zero duration is falsy</b>, any non-zero span is truthy — matching
/// the empty-is-falsy convention of the other scalars. The bare wire form is
/// ISO-8601 (<see cref="ToString"/>).</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this,
    global::app.data.IEquatableValue, global::app.data.IOrderableValue,
    System.IEquatable<@this>
{
    public static string Example => "PT5M";
    public static string Shape => "string";

    public System.TimeSpan Value { get; }
    public override object? ToRaw() => Value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.TimeSpan(Value);

    public @this(System.TimeSpan value) { Value = value; }

    // ---- Parts (behavioral targets of the is-TimeSpan sweep) ----
    public int Days => Value.Days;
    public int Hours => Value.Hours;
    public int Minutes => Value.Minutes;
    public int Seconds => Value.Seconds;
    public double TotalHours => Value.TotalHours;
    public double TotalMinutes => Value.TotalMinutes;
    public double TotalSeconds => Value.TotalSeconds;

    /// <summary>Bare ISO-8601 duration form (e.g. <c>PT1H30M</c>) — the serializer renders this.</summary>
    public override string ToString() => System.Xml.XmlConvert.ToString(Value);

    // ---- Truthiness (item): zero is falsy ----
    public override bool IsTruthy() => Value != System.TimeSpan.Zero;

    // ---- Equality + order (by span length) ----
    public bool AreEqual(object? other) => other switch
    {
        @this d => Value == d.Value,
        System.TimeSpan ts => Value == ts,
        _ => false,
    };

    public int Order(object? other) => other switch
    {
        @this d => Value.CompareTo(d.Value),
        System.TimeSpan ts => Value.CompareTo(ts),
        _ => throw new global::app.data.Compare.NotOrderableException(
            $"cannot order duration against {other?.GetType().Name ?? "null"}"),
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
