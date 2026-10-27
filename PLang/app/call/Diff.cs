namespace app.call;

/// <summary>
/// One captured variable mutation observed during a Call's execution. <c>Before</c> is the
/// pre-mutation value; the current value lives on Variables. For scalar-only capture
/// (default), Before is the raw scalar; for non-scalar values (lists, dicts, objects),
/// Before is a summary string like <c>"&lt;List&lt;int&gt; @ 5042 items&gt;"</c> unless
/// the setting's <c>diff.deep</c> is on, in which case Before is the value's own copy. In a trace it shows its name and when.
/// </summary>
public sealed record Diff
{
    [Out, Debug] public string Name { get; }

    public object? Before { get; }

    [Out, Debug] public DateTimeOffset At { get; }

    /// <summary>A change of <paramref name="name"/> now, its value before it captured by the capture rule: a scalar as it
    /// is, any other value as its summary, or as it was when <paramref name="deep"/> (the diff.deep setting, the error
    /// stream) — a summary by default keeps tight loops over large lists from holding every copy.</summary>
    internal Diff(string name, object? before, bool deep)
    {
        Name = name;
        At = DateTimeOffset.UtcNow;
        Before = before switch
        {
            null => null,
            // a scalar as it is — a short text or a leaf: deeply immutable, so holding the instance is safe
            global::app.type.item.text.@this text when text.Length.ToInt32() <= 256 => text,
            global::app.type.item.@this { IsLeaf: true } leaf and not global::app.type.item.text.@this => leaf,
            // kept as it was: the value's own copy (a container's rows copied, any other value — which no change
            // reaches — as it is), never a walk of everything it reaches: a task's value reaches the whole app
            global::app.type.item.@this value when deep => value.Copy(),
            // else its summary, by its plang type name and count — never the C# class name ("this")
            global::app.type.item.list.@this list => $"<list @ {list.CountRaw} items>",
            global::app.type.item.dict.@this dict => $"<dict @ {dict.CountRaw} items>",
            System.Collections.ICollection collection => $"<{before.GetType().Name} @ {collection.Count} items>",
            _ => $"<{before.GetType().Name}>",
        };
    }
}
