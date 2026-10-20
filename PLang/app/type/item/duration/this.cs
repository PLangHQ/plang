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
[global::app.Attributes.PlangType("duration"), global::app.Attributes.Kinds, global::app.Attributes.Choice]
public abstract partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>,
    global::app.type.item.setting.ISetting<setting.@this>, System.IEquatable<@this>
{
    public static string Shape => "string";

    public System.TimeSpan Value { get; }

    /// <summary>The CLR exit door — the type hands its own backing.</summary>
    internal override object? Clr(System.Type target) => ClrConvert(Value, target);
    public override bool IsLeaf => true;

    /// <summary>A duration writes itself in the standard it is: a <c>short</c> as <c>30s</c>, an <c>iso</c> as
    /// <c>PT5M</c>, a <c>dotnet</c> as <c>00:05:00</c> — each standard a kind of duration, a class of its own.</summary>
    protected internal abstract override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer);

    /// <summary>A span born holding <paramref name="type"/> — the app's {duration, standard}.</summary>
    protected @this(System.TimeSpan value, global::app.type.@this type) : base(type) => Value = value;

    /// <summary>The span <paramref name="raw"/> is in a standard whose text <paramref name="read"/> reads: a span is
    /// itself, a text what the standard reads of it; null when it is neither.</summary>
    protected static System.TimeSpan? Span(object? raw, System.Func<string, System.TimeSpan?> read)
        => (raw is global::app.type.item.@this leaf ? leaf.Clr<object>() : raw) switch
        {
            System.TimeSpan given => given,
            string text => read(text.Trim()),
            _ => null,
        };

    /// <summary>A standard refuses a text in another form, naming itself and how it is written
    /// (<c>'30s' is not a duration in iso (iso is written PT30S)</c>); anything else declines unnamed.</summary>
    protected static void Refuse(object? raw, string standard, string written, global::app.data.@this data)
    {
        if ((raw is global::app.type.item.@this leaf ? leaf.Clr<object>() : raw) is string text)
            data.Fail(new global::app.error.@this($"'{text}' is not a duration in {standard} ({standard} is written {written})", "DurationParseFailed", 400));
    }

    /// <summary>A span with no form of its own (how long a step ran), made by the standard its asker's setting names
    /// (<c>short</c> unless set), through the app's entry for it.</summary>
    public static @this Create(System.TimeSpan span, global::app.actor.context.@this context)
    {
        var standard = context.Setting.Of<setting.@this>().Standard.Value;
        return context.App.type.list[new global::app.type.@this("duration", standard.Name), context]
                   .Make(span, new global::app.data.@this("", context: context)) as @this
               ?? throw new System.InvalidOperationException($"the duration standard {standard.Name} made no span of {span}.");
    }

    /// <summary>THE PURE CORE — a <c>duration</c> passes through; a TimeSpan is made in its asker's standard; a string (30s, ISO-8601
    /// <c>PT30S</c> or .NET <c>00:00:30</c>, via <see cref="Resolve"/>) is the standard it is written in, as
    /// <paramref name="context"/>'s types hold them; anything else declines (<c>null</c>). A text-wrapped literal
    /// unwraps through <c>Clr&lt;object&gt;()</c>. Shared by the ICreate courier and comparison coercion.</summary>
    public static @this? Create(object? raw, global::app.actor.context.@this context)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value switch
        {
            System.TimeSpan ts => Create(ts, context),
            string s => Resolve(s, context),
            _ => null,
        };
    }

    /// <summary>The ICreate courier face — delegates to the pure core; on decline lands the reason
    /// on <paramref name="data"/> (a bad string vs a wrong type).</summary>
    public static @this? Create(object? value, global::app.type.@this? declared, global::app.data.@this data)
    {
        // asked for in a standard (`as iso`): that standard makes it, from a span or a text in its own form only
        if (declared?.kind is { IsEmpty: false } asked && data.Context.App.type.list["duration"].kind[asked.Name] is global::app.type.kind.@this<@this> standard)
            return data.Context.App.type.list[new global::app.type.@this("duration", standard.Name), data.Context].Make(value, data) as @this;
        if (Create(value, data.Context) is { } built) return built;
        data.Fail((((value as global::app.type.item.@this)?.Clr<object>() ?? value) is string s)
            ? new global::app.error.@this($"Cannot parse '{s}' as duration — expected a number and its unit (30s, 200ms, 5m), ISO-8601 (PT30S) or .NET format (00:00:30).", "DurationParseFailed", 400)
            : new global::app.error.@this($"Cannot convert {((value as global::app.type.item.@this)?.Type.Name ?? value?.GetType().Name)} to duration.", "DurationConversionFailed", 400));
        return null;
    }

    // Both directions are lossless; the wrapper owns its conversions. TimeSpan is a
    // value type so `d == null` is unambiguous (matches only @this==@this).
    public static implicit operator System.TimeSpan(@this d) => d.Value;
    public static implicit operator @this(System.TimeSpan t) => new @short.@this(t);

    public static bool operator ==(@this? a, @this? b) => a is null ? b is null : a.Equals(b);
    public static bool operator !=(@this? a, @this? b) => !(a == b);
    public static bool operator ==(@this? a, System.TimeSpan b) => a is not null && a.Value == b;
    public static bool operator !=(@this? a, System.TimeSpan b) => !(a == b);
    public static bool operator ==(System.TimeSpan a, @this? b) => b == a;
    public static bool operator !=(System.TimeSpan a, @this? b) => !(b == a);

    /// <summary>A span holds a number a step wrote when it is the whole span in one of its units — 1s holds 1000 (as
    /// milliseconds) and 1 (as seconds), never 7.</summary>
    internal override async System.Threading.Tasks.ValueTask<bool> Holds(global::app.type.item.number.@this number, global::app.actor.context.@this context)
    {
        if (await base.Holds(number, context)) return true;
        foreach (var unit in new[] { Milliseconds(context), Seconds(context), Minutes(context), Hours(context), Days(context) })
            if ((await unit.Value())?.CompareTo(number) == 0) return true;
        return false;
    }

    // ---- The span in each unit, whole: %elapsed.seconds% is 90 for 1m30s. How a span looks (1h30m) is its
    //      writer's, so there are no component members. Each is the carrier the asker's context births ----
    [LlmBuilder] public global::app.data.@this<global::app.type.item.number.@this> Days(global::app.actor.context.@this context)
        => global::app.data.@this<global::app.type.item.number.@this>.From(context.Ok(Value.TotalDays));
    [LlmBuilder] public global::app.data.@this<global::app.type.item.number.@this> Hours(global::app.actor.context.@this context)
        => global::app.data.@this<global::app.type.item.number.@this>.From(context.Ok(Value.TotalHours));
    [LlmBuilder] public global::app.data.@this<global::app.type.item.number.@this> Minutes(global::app.actor.context.@this context)
        => global::app.data.@this<global::app.type.item.number.@this>.From(context.Ok(Value.TotalMinutes));
    [LlmBuilder] public global::app.data.@this<global::app.type.item.number.@this> Seconds(global::app.actor.context.@this context)
        => global::app.data.@this<global::app.type.item.number.@this>.From(context.Ok(Value.TotalSeconds));
    [LlmBuilder] public global::app.data.@this<global::app.type.item.number.@this> Milliseconds(global::app.actor.context.@this context)
        => global::app.data.@this<global::app.type.item.number.@this>.From(context.Ok(Value.TotalMilliseconds));

    // ---- Truthiness (item): zero is falsy ----
    public override bool IsTruthy() => Value != System.TimeSpan.Zero;

    // ---- Comparison — the value's own behavior (see app.data.Comparison) ----

    /// <summary>Outranks text — ISO-8601 duration text coerces into the duration.</summary>
    public override int Rank => 400;

    /// <summary>Span-length ordering in caller order; the other side coerces into duration through
    /// the pure <c>Create</c> core (ISO text → duration). Non-coercible → Incomparable.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(global::app.type.item.@this other, global::app.actor.context.@this context)
    {
        var b = other as @this ?? Create(other, context);
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
