namespace app.goal.child;

/// <summary>
/// A goal's sub-goals — the goals written after its first in the same file. Taking one makes this goal
/// its parent, wherever it is added from (the build's parse, the <c>.pr</c> reader), so a sub-goal
/// always knows the goal it lives in: its address (<c>/start#show</c>) and its visibility derive from it.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<goal.@this>
{
    private readonly goal.@this _owner;

    internal @this(goal.@this owner, global::app.type.@this type) : base(new List<object?>(), type) => _owner = owner;


    protected override void Admit(object? slot)
    {
        if ((slot is global::app.data.@this held ? held.Peek() : slot) is goal.@this sub) sub.Parent = _owner;
    }

    protected override void Admit(global::app.type.item.list.@this other)
    {
        foreach (var slot in other.Slots()) Admit(slot);
    }

    /// <summary>Writes itself as the bare sub-goal array — each sub-goal writes its own goal shape (not the base's
    /// per-row value face), the shape the goal reader reads.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginArray((int)Count);
        for (int i = 0; i < Count; i++) await this[i].Output(writer);
        writer.EndArray();
    }
}
