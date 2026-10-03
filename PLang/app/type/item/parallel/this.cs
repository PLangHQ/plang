namespace app.type.item.parallel;

/// <summary>
/// PLang <c>parallel</c> value — running side by side: <c>in parallel</c>, or <c>in parallel(cpu: 2)</c>. Its
/// <see cref="Cpu"/> is how many run at once where there are several (a foreach's items, an llm's tool calls); a single
/// call has nothing to cap. Left out, nothing runs in parallel. A program written before it was a type said
/// <c>true</c> or <c>false</c>: true is parallel at its default, false is not parallel.
/// </summary>
[global::app.Attributes.PlangType("parallel")]
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Shape => "object";

    /// <summary>How many run at once — by default the machine's cores × 0.8, at least one.</summary>
    [Out, Store] public global::app.type.item.number.@this Cpu { get; }

    // whether it runs in parallel at all — false only for a program's `false`
    private readonly bool _on;

    /// <summary>Running in parallel, <paramref name="cpu"/> at once; none given, the machine's cores × 0.8, at least
    /// one.</summary>
    public @this(long? cpu = null) : this(cpu ?? System.Math.Max(1, (long)(System.Environment.ProcessorCount * 0.8)), on: true) { }

    private @this(long cpu, bool on)
    {
        Cpu = System.Math.Max(1, cpu);
        _on = on;
    }

    /// <summary>Not running in parallel — what a program's <c>false</c> says.</summary>
    public static @this Off { get; } = new(1, on: false);

    public override bool IsLeaf => false;

    /// <summary>Whether it runs in parallel.</summary>
    public override bool IsTruthy() => _on;

    /// <summary>How many run at once, as a count.</summary>
    internal int At => (int)System.Math.Min(int.MaxValue, Cpu.ToInt64());

    /// <summary>A parallel passes through; true is parallel at its default, false is not parallel; a number is how
    /// many at once; <c>{cpu: 2}</c> says it.</summary>
    public static @this? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data)
    {
        if (raw is @this parallel) return parallel;
        var value = raw is global::app.type.item.@this item && item is not global::app.type.item.dict.@this ? item.Clr<object>() : raw;
        switch (value)
        {
            case bool on: return on ? new @this() : Off;
            case long or int or decimal or double: return new @this(System.Convert.ToInt64(value));
            case string s when bool.TryParse(s, out var on): return on ? new @this() : Off;
            case string s when long.TryParse(s, out var cpu): return new @this(cpu);
        }
        if (raw is global::app.type.item.dict.@this dict && data.Context is { } context)
        {
            long? cpu = null;
            foreach (var entry in dict.Entries(context))
                if (string.Equals(entry.Name, "cpu", System.StringComparison.OrdinalIgnoreCase))
                    cpu = entry.Peek()?.Clr<object>() is { } count ? System.Convert.ToInt64(count) : null;
                else
                {
                    data.Fail(new global::app.error.Error($"parallel's one member is cpu (how many at once) — not {entry.Name}", "ParallelInvalid", 400));
                    return null;
                }
            return new @this(cpu);
        }
        data.Fail(new global::app.error.Error(
            "parallel is written `in parallel`, `parallel(cpu: 2)` or {cpu: 2}", "ParallelInvalid", 400));
        return null;
    }
}
