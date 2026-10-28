namespace app.@event.binding.action;

/// <summary>
/// A binding a program made (<c>on.event</c>): it holds the <c>on.event</c> action that bound it, and runs that action's
/// <c>Action</c> (a goal call) when its event fires. The call reads <c>%!event%</c> — the running event, with <c>!item</c>
/// (what it fired for) and <c>!result</c> (the result so far, its value) — held on the frame the event fired in for
/// exactly as long as the call runs: gone when it returns, each parallel firing (its own frame) sees its own, and a nested
/// event's call its own. No frame (a C#-only firing): no <c>%!event%</c>. A snapshot keeps it: it writes whose it is and
/// the action that bound it, and is made again from that.
/// </summary>
public sealed class @this : global::app.@event.binding.@this
{
    private readonly global::app.@event.@this _event;

    internal @this(global::app.@event.binding.list.@this side, global::app.@event.@this @event,
        global::app.goal.step.action.@this action, global::app.actor.@this actor, global::app.@event.binding.Scope scope)
        : base(side, actor, scope, Always)
    {
        _event = @event;
        this.action = action;
    }

    /// <summary>A binding read back from what it wrote (<see cref="Write"/>): whose it was, and the action that bound it,
    /// each read as its member's type. A row that doesn't read is refused, thrown to the read boundary.</summary>
    internal @this(global::app.@event.binding.list.@this side, global::app.@event.@this @event,
        global::app.type.item.dict.@this record, global::app.actor.context.@this context)
        : this(side, @event, Kept<global::app.goal.step.action.@this>(record, "action", context),
            Kept<global::app.actor.@this>(record, "actor", context),
            Kept<global::app.type.item.choice.@this<global::app.@event.binding.Scope>>(record, "scope", context).Value)
    { }

    /// <summary>The <c>on.event</c> action that bound it; what it runs is that action's <c>Action</c>, read when it fires.</summary>
    internal global::app.goal.step.action.@this action { get; }

    internal override bool IsSaved => true;

    private protected override async Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        var bound = action["action"]?.Data(context);
        if (bound == null || await bound.Value<global::app.goal.step.action.@this>() is not { } call)
            return bound?.Error != null ? bound : context.Error(new global::app.error.@this(
                $"the binding made by '{action.Step?.Text}' runs no action", "BindingNoAction", 400));

        // a property holds a value, never a Data — the result's value; a failed result is %!error%
        var running = new global::app.data.@this("!event", _event, context: context);
        running.Properties.Set("item", item);
        running.Properties.Set("result", result.Peek());

        var frame = context.call.Current;
        if (frame == null) return await call.Start(context);
        var outer = frame.Event;
        frame.Event = running;
        try { return await call.Start(context); }
        finally { frame.Event = outer; }
    }

    /// <summary>It writes whose it is and the action that bound it: <c>{actor, scope, action}</c>, each as its member's
    /// type, the actor by what finds it. The action carries its type: the snapshot's load pass finds the goal it names only
    /// through a typed row. An action that names its call by a variable (<c>Action=%x%</c>) is refused: read back, the
    /// variable would be read again where the event fires.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        if (action["action"]?.Value is { IsVariable: true })
            throw new global::app.error.AppException(new global::app.error.@this(
                $"the binding made by '{action.Step?.Text}' names its call by a variable; a snapshot can't keep it — " +
                "name the call in the step", "SnapshotBindingVariable", 400));
        var context = writer.Context;
        writer.BeginObject();
        writer.Name("actor");
        await Actor.Output(writer);
        writer.Name("scope");
        await ((global::app.type.item.choice.@this<global::app.@event.binding.Scope>)Scope).Output(writer);
        writer.Name("action");
        await new global::app.data.@this("", action, context: context).Output(writer);
        writer.EndObject();
    }

    // the member a saved binding's record keeps under name, read in the snapshot view as its member's type
    private static T Kept<T>(global::app.type.item.dict.@this record, string name, global::app.actor.context.@this context)
        where T : global::app.type.item.@this
        => record.Get(name, context) is { } kept
           && global::app.View.Snapshot.Make(context.App.type.list[typeof(T)], kept.Peek(), context) is T value
            ? value : throw Unread(name);

    private static global::app.error.DeclinedException Unread(string name)
        => new(new global::app.error.@this($"a saved binding keeps no {name} that reads", "SnapshotRowUnread", 400));
}
