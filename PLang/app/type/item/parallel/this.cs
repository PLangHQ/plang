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
    [Out, Store] public global::app.type.item.number.@this Cpu
        => System.Math.Max(1, _cpu ?? (long)(System.Environment.ProcessorCount * 0.8));

    // how many at once the program said, if it said — none is the default of the machine it runs on
    private readonly long? _cpu;

    // whether it runs in parallel at all — false only for a program's `false`
    private readonly bool _on;

    /// <summary>Running in parallel, <paramref name="cpu"/> at once — none given, the machine's cores × 0.8, at least
    /// one — born holding <paramref name="type"/>, the app's parallel.</summary>
    public @this(global::app.type.@this type, long? cpu = null) : this(type, cpu, on: true) { }

    private @this(global::app.type.@this type, long? cpu, bool on) : base(type)
    {
        _cpu = cpu;
        _on = on;
    }

    /// <summary>Not running in parallel — what a program's <c>false</c> says — born holding <paramref name="type"/>.</summary>
    public static @this Off(global::app.type.@this type) => new(type, 1, on: false);

    public override bool IsLeaf => false;

    /// <summary>Its wire form, read back by its reader as itself: <c>false</c> when it is off, else <c>{cpu: n}</c> with
    /// the count the program said, or <c>{}</c> when it said none — so a built program takes the cores of the machine
    /// it runs on, not the one it was built on.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        if (!_on) { writer.Bool(false); return default; }
        writer.BeginObject();
        if (_cpu is { } cpu) { writer.Name("cpu"); writer.Long(cpu); }
        writer.EndObject();
        return default;
    }

    /// <summary>Whether it runs in parallel.</summary>
    public override bool IsTruthy() => _on;

    /// <summary>How many run at once, as a count.</summary>
    internal int At => (int)System.Math.Min(int.MaxValue, Cpu.ToInt64());

    /// <summary>A parallel passes through; true is parallel at its default, false is not parallel; a number is how
    /// many at once; <c>{cpu: 2}</c> says it.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this parallel) return parallel;
        type = data.Context!.App.type.list[typeof(@this)];
        var value = raw is global::app.type.item.@this item && item is not global::app.type.item.dict.@this ? item.Clr<object>() : raw;
        switch (value)
        {
            case bool on: return on ? new @this(type) : Off(type);
            case long or int or decimal or double: return new @this(type, System.Convert.ToInt64(value));
            case string s when bool.TryParse(s, out var on): return on ? new @this(type) : Off(type);
            case string s when long.TryParse(s, out var cpu): return new @this(type, cpu);
        }
        if (raw is global::app.type.item.dict.@this dict && data.Context is { } context)
        {
            if (dict.Entries(context).FirstOrDefault(entry => !string.Equals(entry.Name, "cpu", System.StringComparison.OrdinalIgnoreCase)) is { } other)
            {
                data.Fail(new global::app.error.@this($"parallel's one member is cpu (how many at once) — not {other.Name}", "ParallelInvalid", 400));
                return null;
            }
            return new @this(type, dict.Get<global::app.type.item.number.@this>("cpu", context)?.ToInt64());
        }
        data.Fail(new global::app.error.@this(
            "parallel is written `in parallel`, `parallel(cpu: 2)` or {cpu: 2}", "ParallelInvalid", 400));
        return null;
    }
}
