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
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Example => "PT5M";
    public static string Shape => "string";

    public System.TimeSpan Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.TimeSpan(Value);
    protected internal override global::app.type.@this Mint() => new("duration", typeof(System.TimeSpan));

    public @this(System.TimeSpan value) { Value = value; }

    /// <summary>THE PURE CORE — a <c>duration</c> passes through; a TimeSpan or a string (ISO-8601
    /// <c>PT30S</c> or .NET <c>00:00:30</c>, via <see cref="Resolve"/> whose context is unused)
    /// parses; anything else declines (<c>null</c>). A text-wrapped literal unwraps through
    /// <c>Clr&lt;object&gt;()</c>. Shared by the ICreate courier and comparison coercion.</summary>
    public static @this? Create(global::app.type.item.@this value)
    {
        if (value is @this self) return self;
        return value.Clr<object>() switch
        {
            System.TimeSpan ts => (@this)ts,
            string s => Resolve(s, null!),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a bad string vs a wrong type).</summary>
    public static @this? Create(global::app.type.item.@this value, global::app.data.@this data)
    {
        if (Create(value) is { } built) return built;
        data.Fail(value.Clr<object>() is string s
            ? new global::app.error.Error($"Cannot parse '{s}' as duration — expected ISO-8601 (e.g. PT30S) or .NET format (e.g. 00:00:30).", "DurationParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {value.Mint().Name} to duration.", "DurationConversionFailed", 400));
        return null;
    }

    // Both directions are lossless; the wrapper owns its conversions. TimeSpan is a
    // value type so `d == null` is unambiguous (matches only @this==@this).
    public static implicit operator System.TimeSpan(@this d) => d.Value;
    public static implicit operator @this(System.TimeSpan t) => new(t);

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);
    public static bool operator ==(@this? a, System.TimeSpan b) => a is not null && a.Value == b;
    public static bool operator !=(@this? a, System.TimeSpan b) => !(a == b);
    public static bool operator ==(System.TimeSpan a, @this? b) => b == a;
    public static bool operator !=(System.TimeSpan a, @this? b) => !(b == a);

    // ---- Parts (behavioral targets of the is-TimeSpan sweep) ----
    public int Days => Value.Days;
    public int Hours => Value.Hours;
    public int Minutes => Value.Minutes;
    public int Seconds => Value.Seconds;
    public double TotalHours => Value.TotalHours;
    public double TotalMinutes => Value.TotalMinutes;
    public double TotalSeconds => Value.TotalSeconds;
    public double TotalMilliseconds => Value.TotalMilliseconds;

    /// <summary>Bare ISO-8601 duration form (e.g. <c>PT1H30M</c>) — the serializer renders this.</summary>
    public override string ToString() => System.Xml.XmlConvert.ToString(Value);

    // ---- Truthiness (item): zero is falsy ----
    public override bool IsTruthy() => Value != System.TimeSpan.Zero;

    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Outranks text — ISO-8601 duration text coerces into the duration.</summary>
    internal static int CompareRank => 40;

    /// <summary>Span-length ordering in caller order; the other side coerces through this
    /// family's own Convert hook (ISO text → duration). Non-coercible → Incomparable.</summary>
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

    // ---- Equality + order (by span length) ----
    public bool AreEqual(object? other) => other switch
    {
        @this d => Value == d.Value,
        System.TimeSpan ts => Value == ts,
        _ => false,
    };

    public bool Equals(@this? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => Equals(obj as @this);
    public override int GetHashCode() => Value.GetHashCode();
}
