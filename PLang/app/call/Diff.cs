namespace app.call;

/// <summary>
/// One captured variable mutation observed during a Call's execution. <c>Before</c> is the
/// pre-mutation value; the current value lives on Variables. For scalar-only capture
/// (default), Before is the raw scalar; for non-scalar values (lists, dicts, objects),
/// Before is a summary string like <c>"&lt;List&lt;int&gt; @ 5042 items&gt;"</c> unless
/// the setting's <c>diff.deep</c> is on, in which case Before is the value's own copy. In a trace it shows its name and when.
/// </summary>
public sealed record Diff([property: Out, Debug] string Name, object? Before, [property: Out, Debug] DateTimeOffset At)
{
    /// <summary>A change of <paramref name="name"/> now, its value before it captured by the capture rule: a scalar as it
    /// is, any other value as its summary, or as it was when <paramref name="deep"/> (the diff.deep setting, the error
    /// stream) — a summary by default keeps tight loops over large lists from holding every copy.</summary>
    internal Diff(string name, object? before, bool deep) : this(name, null, DateTimeOffset.UtcNow)
        => Before = Captured(before, deep);

    // A value kept as it was is its own copy (item.Copy: a container's rows copied, any other value — which no change
    // reaches — as it is), never a walk of everything it reaches: a task's value reaches the whole app.
    private object? Captured(object? value, bool deep)
    {
        if (value == null) return null;
        if (IsScalar(value)) return value;
        if (deep && value is global::app.type.item.@this item) return item.Copy();
        return Summary(value);
    }

    // What a store records as before is a value (a Data's Peek): a scalar is a short text or a leaf. Scalar wrappers are
    // deeply immutable — holding the instance as Before is safe, no clone needed.
    private bool IsScalar(object value) =>
        value switch
        {
            global::app.type.item.text.@this t => t.Length.ToInt32() <= 256,
            global::app.type.item.@this i => i.IsLeaf,
            _ => false
        };

    private string Summary(object value)
    {
        // A native plang collection summarises by its plang type name and item
        // count — never the C# class name ("this"), which leaks the internal type
        // into user-facing debug output.
        if (value is global::app.type.item.list.@this list)
            return $"<list @ {list.CountRaw} items>";
        if (value is global::app.type.item.dict.@this dict)
            return $"<dict @ {dict.CountRaw} items>";
        var t = value.GetType();
        if (value is System.Collections.ICollection col)
            return $"<{t.Name} @ {col.Count} items>";
        return $"<{t.Name}>";
    }
}
