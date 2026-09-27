namespace app.@event;

/// <summary>
/// One event of one object — a verb's (<c>start</c>, <c>create</c>, …) or an outcome's (<c>error</c>,
/// <c>hit</c>, <c>miss</c>): the bindings started before it and after it. One class per event
/// (<c>app/event/on/&lt;name&gt;.cs</c>); the item an event fires for is handed to its bindings when it starts,
/// never kept (one event serves every item of a type).
/// </summary>
public abstract class @this : global::app.type.item.@this
{
    private protected @this(string name, binding.list.@this before, binding.list.@this after)
    {
        Name = name;
        this.before = before;
        this.after = after;
    }

    /// <summary>The event's name — the verb's own (<c>start</c> for <c>Start</c>), or the outcome's.</summary>
    public string Name { get; }

    /// <summary>What is started before it.</summary>
    public binding.list.@this before { get; }

    /// <summary>What is started after it.</summary>
    public binding.list.@this after { get; }

    /// <summary>Every event is of the type <c>event</c>, whichever its class.</summary>
    protected internal override global::app.type.@this Type => new("event", typeof(@this));

    public override string ToString() => Name;
}
