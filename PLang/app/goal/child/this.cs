namespace app.goal.child;

/// <summary>
/// A goal's sub-goals — the goals written after its first in the same file. Taking one makes this goal
/// its parent, wherever it is added from (the build's parse, the <c>.pr</c> reader), so a sub-goal
/// always knows the goal it lives in: its address (<c>/start#show</c>) and its visibility derive from it.
/// </summary>
public sealed class @this : global::app.type.item.list.@this<goal.@this>
{
    private readonly goal.@this _owner;

    internal @this(goal.@this owner) : base(new List<object?>()) => _owner = owner;

    protected override global::app.type.item.list.@this Empty() => new @this(_owner);

    protected override void Admit(object? slot)
    {
        if ((slot is global::app.data.@this held ? held.Peek() : slot) is goal.@this sub) sub.Parent = _owner;
    }

    protected override void Admit(global::app.type.item.list.@this other)
    {
        foreach (var slot in other.Slots()) Admit(slot);
    }
}
