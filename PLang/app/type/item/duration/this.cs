namespace app.type.item.duration;

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
[global::app.Attributes.PlangType("duration")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    System.IEquatable<@this>
{
    public static string Shape => "string";

    public System.TimeSpan Value { get; }

    // the standard it is written in (short, iso, dotnet), born with it; and its type, {duration, <kind>}, made once
    private readonly kind.@this _kind;
    private readonly global::app.type.@this _type;

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;

    /// <summary>Its text in its kind — <c>30s</c>, <c>PT5M</c>; <c>%d.text%</c>.</summary>
    public string Text => _kind.Text(Value);

    /// <summary>A duration writes its text in its own kind: <c>30s</c> stays <c>30s</c>, <c>PT5M</c> stays <c>PT5M</c>.</summary>
    public override void Write(global::app.type.format.IWriter w) => w.Content(Text, _kind);
    protected internal override global::app.type.@this Type => _type;

    /// <summary>A span made with no text (a C# span, an elapsed time) — written short, the form a step reads.</summary>
    public @this(System.TimeSpan value) : this(value, new kind.@short.@this()) { }

    /// <summary>A span in <paramref name="kind"/>'s standard.</summary>
    public @this(System.TimeSpan value, kind.@this kind)
    {
        Value = value;
        _kind = kind;
        _type = new global::app.type.@this("duration", typeof(@this), kind.Name);
    }

    /// <summary>THE PURE CORE — a <c>duration</c> passes through; a TimeSpan or a string (ISO-8601
    /// <c>PT30S</c> or .NET <c>00:00:30</c>, via <see cref="Resolve"/> whose context is unused)
    /// parses; anything else declines (<c>null</c>). A text-wrapped literal unwraps through
    /// <c>Clr&lt;object&gt;()</c>. Shared by the ICreate courier and comparison coercion.</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value switch
        {
            System.TimeSpan ts => (@this)ts,
            string s => Resolve(s, null!),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a bad string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (Create(value) is { } built)
        {
            // asked for in a standard (`as iso`): the same span, written in that one
            if (declared?.kind is { IsEmpty: false } asked && !string.Equals(asked.Name, built._kind.Name, System.StringComparison.OrdinalIgnoreCase)
                && data.Context?.App.type.list["duration"].kind[asked.Name] is kind.@this standard)
                return new @this(built.Value, standard);
            return built;
        }
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.Error($"Cannot parse '{s}' as duration — expected a number and its unit (30s, 200ms, 5m), ISO-8601 (PT30S) or .NET format (00:00:30).", "DurationParseFailed", 400)
            : new global::app.error.Error($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to duration.", "DurationConversionFailed", 400));
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

    /// <summary>A span holds a number a step wrote when it is the whole span in one of its units — 1s holds 1000 (as
    /// milliseconds) and 1 (as seconds), never 7.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(global::app.type.item.number.@this number, global::app.actor.context.@this context)
        => await base.Holds(number, context)
           || new[] { Milliseconds, Seconds, Minutes, Hours, Days }.Any(unit => unit.CompareTo(number) == 0);

    // ---- The span in each unit, whole: %elapsed.seconds% is 90 for 1m30s. How a span looks (1h30m) is its
    //      writer's, so there are no component members ----
    [LlmBuilder] public global::app.type.item.number.@this Days => Value.TotalDays;
    [LlmBuilder] public global::app.type.item.number.@this Hours => Value.TotalHours;
    [LlmBuilder] public global::app.type.item.number.@this Minutes => Value.TotalMinutes;
    [LlmBuilder] public global::app.type.item.number.@this Seconds => Value.TotalSeconds;
    [LlmBuilder] public global::app.type.item.number.@this Milliseconds => Value.TotalMilliseconds;

    /// <summary>Its text in its kind (<see cref="Text"/>).</summary>
    public override string ToString() => Text;

    // ---- Truthiness (item): zero is falsy ----
    public override bool IsTruthy() => Value != System.TimeSpan.Zero;

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks text — ISO-8601 duration text coerces into the duration.</summary>
    public override int Rank => 400;

    /// <summary>Span-length ordering in caller order; the other side coerces into duration through
    /// the pure <c>Create</c> core (ISO text → duration). Non-coercible → Incomparable.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        var b = other as @this ?? Create(other);
        if (b is null) return new(global::app.data.Comparison.Incomparable);
        var c = Value.CompareTo(b.Value);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }

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
