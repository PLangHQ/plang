namespace app.type.date;

/// <summary>
/// PLang <c>date</c> value — its own type, backed by <see cref="System.DateOnly"/>.
/// <b>Distinct from <c>datetime</c></b>: the historical collapse (ScalarComparer
/// coerced <c>DateOnly → DateTimeOffset</c> and classed it <c>datetime</c>) ends
/// with this wrapper. Order/equality are day-precision; the bare wire form is
/// ISO <c>yyyy-MM-dd</c>.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this,
    global::app.data.IEquatableValue, global::app.data.IOrderableValue,
    System.IEquatable<@this>
{
    public static string Example => "2024-03-15";
    public static string Shape => "string";

    public System.DateOnly Value { get; }
    public override object? ToRaw() => Value;

    public @this(System.DateOnly value) { Value = value; }

    public int Year => Value.Year;
    public int Month => Value.Month;
    public int Day => Value.Day;

    /// <summary>Bare ISO date form (<c>yyyy-MM-dd</c>) — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public bool AreEqual(object? other) => other switch
    {
        @this d => Value == d.Value,
        System.DateOnly d => Value == d,
        _ => false,
    };

    public int Order(object? other) => other switch
    {
        @this d => Value.CompareTo(d.Value),
        System.DateOnly d => Value.CompareTo(d),
        _ => throw new global::app.data.Compare.NotOrderableException(
            $"cannot order date against {other?.GetType().Name ?? "null"}"),
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
