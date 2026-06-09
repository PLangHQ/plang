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
    System.IEquatable<@this>
{
    public static string Example => "2024-03-15";
    public static string Shape => "string";

    public System.DateOnly Value { get; }
    public override object? ToRaw() => Value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(ToString());

    public @this(System.DateOnly value) { Value = value; }

    public int Year => Value.Year;
    public int Month => Value.Month;
    public int Day => Value.Day;

    /// <summary>Bare ISO date form (<c>yyyy-MM-dd</c>) — the serializer renders this.</summary>
    public override string ToString() =>
        Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Date family outranks text — ISO text coerces into the date, not vice versa.</summary>
    internal static int CompareRank => 50;

    /// <summary>Ordered comparison in caller order; the other side coerces through this
    /// family's own Convert hook (ISO text → date). Non-coercible → Incomparable.</summary>
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
               v is global::app.type.item.@this { IsLeaf: true } l ? l.ToRaw() : v, null, null)?.Peek() as @this;

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
