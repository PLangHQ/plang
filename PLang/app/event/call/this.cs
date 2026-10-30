namespace app.@event.call;

/// <summary>
/// An event a program made (<c>on.event</c>): it holds the <c>on.event</c> action that bound it, and runs that action's
/// <c>Action</c> (a goal call) when its moment comes. The call reads <c>%!event%</c> — the running event, with
/// <c>!item</c> (what it ran for) and <c>!data</c> (the value so far; a failure is <c>%!error%</c>) — held on the frame
/// the moment came in for exactly as long as the call runs: gone when it returns, each parallel run (its own frame) sees
/// its own, and a nested event's call its own. No frame (a C#-only run): no <c>%!event%</c>. A snapshot keeps it: it
/// writes whose it is and the action that bound it, and is made again from that.
/// </summary>
public sealed class @this : global::app.@event.@this
{
    /// <summary>The event <paramref name="action"/> (an <c>on.event</c> step's) binds on <paramref name="item"/>'s moment
    /// <paramref name="name"/>, <paramref name="when"/>, for <paramref name="actor"/>; a sub app takes it in as
    /// <paramref name="sub"/> says.</summary>
    internal @this(global::app.type.item.@this item, string name, global::app.@event.When when,
        global::app.goal.step.action.@this action, global::app.actor.@this actor, global::app.@event.Sub sub)
        : base(item, name, when, actor)
    {
        this.action = action;
        _sub = sub;
    }

    /// <summary>An event read back from what it wrote (<see cref="Write"/>), bound on <paramref name="item"/>'s moment
    /// <paramref name="name"/>, <paramref name="when"/>: whose it was, the action that bound it, each read as its
    /// member's type, and whether a sub app takes it in (inherit when the record keeps none). A row that doesn't read is
    /// refused, thrown to the read boundary.</summary>
    internal @this(global::app.type.item.@this item, string name, global::app.@event.When when,
        global::app.type.item.dict.@this record, global::app.actor.context.@this context)
        : this(item, name, when, Member<global::app.goal.step.action.@this>(record, "action", context),
            Member<global::app.actor.@this>(record, "actor", context), Sub(record, context))
    { }

    // whether a sub app takes it in, as the on.event step said
    private readonly global::app.@event.Sub _sub;

    internal override bool IsInheritable => _sub == global::app.@event.Sub.inherit;

    // the Sub a saved event's record keeps, read as the choice it is; none kept is the default, inherit
    private static global::app.@event.Sub Sub(global::app.type.item.dict.@this record, global::app.actor.context.@this context)
    {
        if (record.Get("sub", context) is not { } row) return global::app.@event.Sub.inherit;
        global::app.@event.Sub sub = global::app.type.item.choice.@this<global::app.@event.Sub>.Create(row.Peek()?.ToString(),
                context.App.type.list[typeof(global::app.type.item.choice.@this<global::app.@event.Sub>)])
            ?? throw Unread($"a saved event keeps a sub '{row.Peek()}' that is none of inherit, none");
        return sub;
    }

    /// <summary>The <c>on.event</c> action that bound it; what it runs is that action's <c>Action</c>, read when it runs.</summary>
    internal override global::app.goal.step.action.@this action { get; }

    internal override bool IsSaved => true;

    internal override async Task<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        var data = action["action"]?.Data(context);
        if (data == null || await data.Value<global::app.goal.step.action.@this>() is not { } call)
            return data?.Error != null ? data : context.Error(new global::app.error.@this(
                $"the event made by '{action.Step?.Text}' runs no action", "EventNoAction", 400));
        if (call["name"]?.Value is not { } value)
            return context.Error(new global::app.error.@this(
                $"the event made by '{action.Step?.Text}' runs {call.Module.Name}.{call.Name}: on.event runs a goal call", "EventNoGoalCall", 400));

        // the goal it calls is found where it was bound: in its own app, from the goal its step sits in — never one the
        // running app, or the running goal's folder, would find first. Its name is read as written (a %variable% in it
        // here, where the moment came, as its parameters are), never selected as a goal in the running context.
        var name = new global::app.data.@this("name", value, context: context);
        var called = (await name.Value())?.ToString() ?? "";
        var goal = await Actor.App.goal.list.Find(called, action.Step?.Goal);
        if (!goal.Success) return goal;
        // the call as it was written, whole — every property its step gave it (its parameters, its actor, whether it runs
        // on its own) and what its build froze — with only the goal it names found here: the seed holds the name alone
        var composed = new global::app.goal.step.action.composed.@this(new global::app.module.goal.Call(context) { Name = goal }, context)
        {
            Property = call.Property,
            Default = call.Default,
        };

        // a property holds a value, never a Data — the value so far; a failed result is %!error%
        var running = new global::app.data.@this("!event", this, context: context);
        running.Properties.Set("item", item);
        running.Properties.Set("data", result.Peek());

        var frame = context.call.Current;
        if (frame == null) return await composed.Start(context);
        var outer = frame.Event;
        frame.Event = running;
        try { return await composed.Start(context); }
        finally { frame.Event = outer; }
    }

    /// <summary>It writes whose it is, the action that bound it and whether a sub app takes it in: <c>{actor, action,
    /// sub}</c>, each as its member's
    /// type, the actor by what finds it. The action carries its type: the snapshot's load pass finds the goal it names only
    /// through a typed row. An action that names its call by a variable (<c>Action=%x%</c>) is refused: read back, the
    /// variable would be read again where the moment comes.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        if (action["action"]?.Value is { IsVariable: true })
            throw new global::app.error.AppException(new global::app.error.@this(
                $"the event made by '{action.Step?.Text}' names its call by a variable; a snapshot can't keep it — " +
                "name the call in the step", "SnapshotEventVariable", 400));
        var context = writer.Context;
        writer.BeginObject();
        writer.Name("actor");
        await Actor.Output(writer);
        writer.Name("action");
        await new global::app.data.@this("", action, context: context).Output(writer);
        writer.Name("sub");
        writer.String(_sub.ToString());
        writer.EndObject();
    }

    // the member a saved event's record keeps under name, read in the snapshot view as its member's type
    private static T Member<T>(global::app.type.item.dict.@this record, string name, global::app.actor.context.@this context)
        where T : global::app.type.item.@this
        => record.Get(name, context) is { } row
           && global::app.View.Snapshot.Make(context.App.type.list[typeof(T)], row.Peek(), context) is T value
            ? value : throw Unread($"a saved event keeps no {name} that reads");
}
