namespace app.type.time;

/// <summary>
/// PLang <c>time</c> value — its own type, backed by <see cref="System.TimeOnly"/>.
/// Today <c>ScalarComparer</c> has no <c>TimeOnly</c> arm at all (time is
/// unhandled); this wrapper closes that gap. Order/equality are within
/// time-of-day; the bare wire form is ISO <c>HH:mm:ss[.fffffff]</c>.
/// </summary>
public sealed partial class @this : global::app.type.item.@this,
    global::app.data.IEquatableValue, global::app.data.IOrderableValue,
    System.IEquatable<@this>
{
    public static string Example => "10:30:00";
    public static string Shape => "string";

    public System.TimeOnly Value { get; }

    public @this(System.TimeOnly value) { Value = value; }

    public int Hour => Value.Hour;
    public int Minute => Value.Minute;
    public int Second => Value.Second;

    /// <summary>Bare ISO time form — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("HH:mm:ss.fffffff", System.Globalization.CultureInfo.InvariantCulture);

    public bool AreEqual(object? other) => other switch
    {
        @this t => Value == t.Value,
        System.TimeOnly t => Value == t,
        _ => false,
    };

    public int Order(object? other) => other switch
    {
        @this t => Value.CompareTo(t.Value),
        System.TimeOnly t => Value.CompareTo(t),
        _ => throw new global::app.data.Compare.NotOrderableException(
            $"cannot order time against {other?.GetType().Name ?? "null"}"),
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
