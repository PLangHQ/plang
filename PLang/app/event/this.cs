namespace app.@event;

/// <summary>
/// What is bound: run before or after a moment of the item it is bound on (<see cref="Name"/>, <see cref="When"/>,
/// <see cref="Item"/>), by the actor that bound it. Each kind runs its own way: a program's goal call
/// (<see cref="call.@this"/>), an action's clause (<see cref="clause.@this"/>), C# (<see cref="code.@this"/>), a maker's
/// taken in by a sub app (<see cref="inherited.@this"/>). Taking it off is its own verb.
/// </summary>
[global::app.Attributes.PlangType("event")]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The filter of an event that runs for every item.</summary>
    internal static readonly System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> Always = (_, _) => true;

    private readonly on.side _side;
    private readonly System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> _filter;

    private protected @this(on.side side, global::app.actor.@this actor, Scope scope,
        System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> filter)
    {
        _side = side;
        _filter = filter;
        Actor = actor;
        Scope = scope;
    }

    /// <summary>An event taken in from <paramref name="event"/> on <paramref name="side"/>, for <paramref name="actor"/>:
    /// its scope and the items it runs for are <paramref name="event"/>'s.</summary>
    private protected @this(on.side side, @this @event, global::app.actor.@this actor)
        : this(side, actor, @event.Scope, @event._filter) { }

    /// <summary>The moment it runs on: <c>start</c>, <c>load</c>, <c>error</c>, <c>create</c> …</summary>
    public string Name => _side.on.Name;

    /// <summary>Before the moment, or after it.</summary>
    public When When => _side.When;

    /// <summary>What it is bound on: a type (each goal), one goal, a catalog action, a module, a channel.</summary>
    public global::app.type.item.@this Item => _side.on.item!;

    /// <summary>The actor that bound it.</summary>
    public global::app.actor.@this Actor { get; }

    /// <summary>Whose it is: the actor's, or the app's.</summary>
    public Scope Scope { get; }

    /// <summary>Whether a snapshot keeps it: an event a program made, which it can make again from what it wrote. A C#
    /// event (a handler) is not: its maker binds it again.</summary>
    internal virtual bool IsSaved => false;

    /// <summary>The <c>on.event</c> action that made it — a program's event is numbered by it (its goal, step and
    /// index). A C# event has none.</summary>
    internal virtual global::app.goal.step.action.@this? action => null;

    /// <summary>Whether it runs for <paramref name="item"/> while <paramref name="context"/>'s actor runs: its scope takes
    /// the actor, and its filter the item.</summary>
    internal bool For(global::app.type.item.@this item, global::app.actor.context.@this context)
        => (Scope == Scope.app || ReferenceEquals(context.Actor, Actor)) && _filter(item, context);

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
    /// <c>on.event</c> action that made it and its place among that action's events in the asker's app —
    /// <c>{action, n}</c>, read back as that event. A C# event has no action and is never pointed at.</summary>
    internal override System.Threading.Tasks.ValueTask<global::app.type.item.@this?> Key(global::app.actor.context.@this context)
    {
        if (action is not { } made) return new((global::app.type.item.@this?)null);
        var n = context.App.@event.Of(made).ToList().IndexOf(this);
        return new(new global::app.type.item.dict.@this(context.App.type.list[typeof(global::app.type.item.dict.@this)])
            .Set(new global::app.data.@this("action", made, context: context)).Set("n", n));
    }

    /// <summary>Every event is of the event type (<c>event</c>), whichever its kind: what a snapshot writes where one is
    /// pointed at is read back by the event type's own reader, from its key.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>An event holds its actor and the side it is on: a copy of the Data holding it shares it.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>Takes it off its moment: it runs no more. A kind that isn't its holder's to take off refuses.</summary>
    public virtual void Remove() => _side.Remove(this);

    public override string ToString() => $"{When} {Name}";
}
