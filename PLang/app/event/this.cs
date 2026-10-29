namespace app.@event;

/// <summary>
/// What is bound: run before or after a moment of the item it is bound on (<see cref="Name"/>, <see cref="When"/>,
/// <see cref="Item"/>), by the actor that bound it, whose events hold it (<see cref="list.@this"/>). Each kind runs its
/// own way: a program's goal call (<see cref="call.@this"/>), an action's clause (<see cref="clause.@this"/>), C#
/// (<see cref="code.@this"/>), a maker's taken in by a sub app (<see cref="inherited.@this"/>). Taking it off is its own
/// verb.
/// </summary>
[global::app.Attributes.PlangType("event")]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private readonly on.side _side;

    private protected @this(on.side side, global::app.actor.@this actor)
    {
        _side = side;
        Actor = actor;
        Each = side.on.item as global::app.type.@this;
    }

    /// <summary>The moment it runs on: <c>start</c>, <c>load</c>, <c>error</c>, <c>create</c> …</summary>
    [global::app.LlmBuilder]
    public string Name => _side.on.Name;

    /// <summary>Before the moment, or after it.</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.choice.@this<When> When => _side.When;

    /// <summary>What it is bound on: a type (each goal), one goal, a catalog action, a module, a channel.</summary>
    public global::app.type.item.@this Item => _side.on.item!;

    /// <summary>The actor that bound it.</summary>
    public global::app.actor.@this Actor { get; }

    /// <summary>The type it is bound on, when it is bound on each item of one (<c>%!app.type.goal.on.start%</c>): the
    /// lists an item makes take it in when the item is of that type. Null when it is bound on one item, whose own moments
    /// it is placed into.</summary>
    internal global::app.type.@this? Each { get; }

    /// <summary>Whether a snapshot keeps it: an event a program made, which it can make again from what it wrote. A C#
    /// event (a handler) is not: its maker binds it again.</summary>
    internal virtual bool IsSaved => false;

    /// <summary>The <c>on.event</c> action that made it — a program's event is numbered by it (its goal, step and
    /// index). A C# event has none.</summary>
    internal virtual global::app.goal.step.action.@this? action => null;

    /// <summary>Runs it for the item its moment came for, on the result as it stands, in the asker's context. An event
    /// doesn't run inside its own run (what it starts may reach its moment again): there it answers a plain success.</summary>
    internal async System.Threading.Tasks.Task<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        if (!context.TryEnterEvent(this)) return context.Ok();
        try { return await Handle(item, result, context, this); }
        finally { context.ExitEvent(this); }
    }

    /// <summary>What it does when its moment comes for <paramref name="item"/>, on the result as it stands, as
    /// <paramref name="event"/> — the event running here (itself, or the sub app's that took it in). Each kind has its
    /// own.</summary>
    internal abstract System.Threading.Tasks.Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context, @this @event);

    /// <summary>What finds a program's event where it is pointed at (a running call's <c>%!event%</c>): the
    /// <c>on.event</c> action that made it and its place among that action's events in its actor's events —
    /// <c>{action, n}</c>, read back as that event. A C# event has no action and is never pointed at.</summary>
    internal override System.Threading.Tasks.ValueTask<global::app.type.item.@this?> Key(global::app.actor.context.@this context)
    {
        if (action is not { } made) return new((global::app.type.item.@this?)null);
        var n = Actor.@event.Of(made).ToList().IndexOf(this);
        return new(new global::app.type.item.dict.@this(context.App.type.list[typeof(global::app.type.item.dict.@this)])
            .Set(new global::app.data.@this("action", made, context: context)).Set("n", n));
    }

    /// <summary>Every event is of the event type (<c>event</c>), whichever its kind: what a snapshot writes where one is
    /// pointed at is read back by the event type's own reader, from its key.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>An event holds its actor and the side it is on: a copy of the Data holding it shares it.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>Takes it off: it runs no more, on any moment it was in. A kind that isn't its holder's to take off
    /// refuses.</summary>
    public virtual void Remove() => Actor.@event.Remove(this);
}
