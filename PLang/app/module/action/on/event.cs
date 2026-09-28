namespace app.module.action.on;

/// <summary>
/// Binds a call on an event: <see cref="Action"/> runs <see cref="When"/> (before or after) <see cref="Event"/> —
/// an item's event, reached by its path: <c>%!app.type.goal.on.start%</c> (each goal starting),
/// <c>%!app.module.file.read.on.start%</c> (each file.read), <c>%!channels.audit.on.write%</c> (a channel writing).
/// The call reads <c>%!event%</c>: the event, with <c>%!event!item%</c> (what it fired for) and
/// <c>%!event!result%</c> (the result so far) — its own, for as long as it runs. Bound for this actor unless
/// <see cref="Scope"/> is app. Answers the binding — <c>on.unbind</c> takes it off.
/// </summary>
[Action("event", Cacheable = false)]
public partial class OnEvent : IContext
{
    /// <summary>The event it binds on — an item's, by its path (<c>…on.start</c>, <c>…on.write</c>).</summary>
    [IsNotNull]
    public partial data.@this<global::app.@event.@this> Event { get; init; }

    /// <summary>Before the event, or after it.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<global::app.@event.When>> When { get; init; }

    /// <summary>The call run when it fires — a <c>goal.call</c> action, held whole.</summary>
    [IsNotNull]
    public partial data.@this<global::app.goal.step.action.@this> Action { get; init; }

    /// <summary>Whose the binding is: this actor's (it fires while this actor runs), or the app's.</summary>
    [Default(global::app.@event.binding.Scope.actor)]
    public partial data.@this<global::app.type.item.choice.@this<global::app.@event.binding.Scope>> Scope { get; init; }

    public async Task<data.@this<global::app.@event.binding.@this>> Start()
    {
        // The path navigates event ← its on ← the item. An item nothing is bound on answers the shared empty
        // events, which don't know their item: the item is the navigation's, and it binds on its own events.
        var reached = Event.IsVariable ? await Event.Get(Context) ?? Event : Event;
        if (reached.Peek() is not global::app.@event.@this named || reached.Parent?.Parent?.Peek() is not global::app.type.item.@this item)
            return Context.Error<global::app.@event.binding.@this>(new global::app.error.ActionError(
                "on.event binds on an item's event, reached by its path — e.g. %!app.type.goal.on.start%", "EventNotFound", 404));

        var own = item.Own();
        var @event = own[named.Name]!;
        global::app.@event.When when = await When.Value();
        global::app.@event.binding.Scope scope = await Scope.Value();
        var call = (await Action.Value())!;
        var binding = own.Bind(named.Name, when, async (fired, result, context) =>
        {
            // %!event% is the running event; what it fired for and the result so far (its value — a property
            // holds a value, never a Data; a failed result is %!error%) are this firing's own. It lives on the
            // frame the event fired in, for exactly as long as the call runs: gone when it returns, and each
            // parallel firing (its own frame) sees its own. No frame (a C#-only firing): no %!event%.
            var running = new global::app.data.@this("!event", @event, context: context);
            running.Properties.Set("item", fired);
            running.Properties.Set("result", result.Peek());
            var frame = context.CallStack.Current;
            if (frame == null) return await call.Start(context);
            var outer = frame.Event;
            frame.Event = running;
            try { return await call.Start(context); }
            finally { frame.Event = outer; }
        }, Context.Actor!, scope);
        return Context.Ok<global::app.@event.binding.@this>(binding);
    }
}
