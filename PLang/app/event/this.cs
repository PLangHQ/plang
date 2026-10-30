namespace app.@event;

/// <summary>
/// What is bound: run before or after a moment (<see cref="Name"/>, <see cref="When"/>) of what it is bound on
/// (<see cref="Item"/>), by the actor that bound it, whose events hold it (<see cref="list.@this"/>). It runs for every
/// item its Item has (<see cref="global::app.type.item.@this.Has"/>). Each kind runs its own way: a program's goal call
/// (<see cref="call.@this"/>), an action's clause (<see cref="clause.@this"/>), C# (<see cref="code.@this"/>), a maker's
/// taken in by a sub app (<see cref="inherited.@this"/>). Taking it off is its own verb.
/// </summary>
[global::app.Attributes.PlangType("event")]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private protected @this(global::app.type.item.@this item, string name, When when, global::app.actor.@this actor)
    {
        Item = item;
        Name = name;
        When = when;
        Actor = actor;
    }

    /// <summary>The moment it runs on: <c>start</c>, <c>load</c>, <c>error</c>, <c>create</c> …</summary>
    [global::app.LlmBuilder]
    public string Name { get; }

    /// <summary>Before the moment, or after it.</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.choice.@this<When> When { get; }

    /// <summary>What it is bound on: a type (each goal), one goal, a pattern of goals, a catalog action, a module, a
    /// channel, a variable's name. It answers which items the event runs for.</summary>
    public global::app.type.item.@this Item { get; }

    /// <summary>The actor that bound it.</summary>
    public global::app.actor.@this Actor { get; }

    /// <summary>Whether a snapshot keeps it: an event a program made, which it can make again from what it wrote. A C#
    /// event (a handler) is not: its maker binds it again.</summary>
    internal virtual bool IsSaved => false;

    /// <summary>Whether a sub app takes it in, when its events first load: every event does, unless it was bound to stay
    /// in its own app (on.event's <c>Sub=none</c>).</summary>
    internal virtual bool IsInheritable => true;

    /// <summary>The app it belongs to: its actor's. A maker's event a sub app took in is its maker's app's.</summary>
    internal virtual global::app.@this App => Actor.App;

    /// <summary>Whether it is left out inside <paramref name="goal"/> under <c>event.setting.goal</c> app: it is bound on
    /// many (a type, a pattern, a module) and the goal is one of plang's own for it — under <c>system/</c>, and the os's
    /// (of no app) or its own app's copy. A sub app's <c>system/</c> goals are its own code, never plang's for its
    /// maker's events; a sub app's own events run inside the <c>system/</c> goals it takes from its maker (a request
    /// app's), as those are its maker's app's.</summary>
    internal bool Skips(global::app.goal.@this goal)
        => Item.IsMany && goal.IsSystem && (goal.App is not { } app || app.Equals(App));

    /// <summary>Whether it takes a failure <paramref name="raised"/> raised while it ran: one nothing raised, or one an
    /// event of its own app raised — a maker's event's failure is for its maker's error events, never a sub app's
    /// own.</summary>
    internal bool Takes(global::app.@event.@this? raised) => raised is null || raised.App.Equals(App);

    /// <summary>The <c>on.event</c> action that made it — a program's event is numbered by it (its goal, step and
    /// index). A C# event has none.</summary>
    internal virtual global::app.goal.step.action.@this? action => null;

    /// <summary>Whether its <c>on.event</c> step sits in the goal at <paramref name="address"/>, the app-absolute address
    /// (<c>/system/event/Hello</c>) — a system event's file, which a sub app's own file at that address replaces. A C#
    /// event sits in none.</summary>
    internal bool IsBoundIn(string address)
        => string.Equals(action?.Step?.Goal.Address, address, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refusal of a saved event, or a saved item's events, that doesn't read back: thrown to the read
    /// boundary.</summary>
    internal static global::app.error.DeclinedException Unread(string message)
        => new(new global::app.error.@this(message, "SnapshotRowUnread", 400));

    /// <summary>Runs it for <paramref name="item"/>, the item its moment came for, on the result as it stands, in the
    /// asker's context. Each kind runs its own way; whoever runs it enters it on the context first
    /// (<see cref="global::app.actor.context.@this.Enter"/>), so it never runs inside its own run.</summary>
    internal abstract System.Threading.Tasks.Task<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context);

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
    protected internal override global::app.type.@this Type => Actor.App.type.list[typeof(@this)];

    /// <summary>An event holds its actor and what it is bound on: a copy of the Data holding it shares it.</summary>
    protected internal override global::app.type.item.@this Clone() => this;

    /// <summary>Takes it off: it runs no more, in any list it was in. A kind that isn't its holder's to take off
    /// refuses.</summary>
    public virtual void Remove() => Actor.@event.Remove(this);
}
