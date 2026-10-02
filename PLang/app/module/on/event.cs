namespace app.module.on;

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

    /// <summary>
    /// Build-time check: the event's path is walked hop by hop. A hop that names no event of an item's events
    /// (<c>…on.before</c>) is refused — every item has the same events, so it can never bind. Any other hop that
    /// reaches nothing surfaces a warning on Channel("builder") naming it — the item may only exist at run (a channel
    /// a step before it creates).
    /// </summary>
    public async Task<data.@this> Build()
    {
        if (__action?["Event"]?.Value?.Variable is not [{ } path, ..]) return Context.Ok();

        data.@this? reached = null;
        foreach (var hop in path.Code.Items())
        {
            var on = reached?.Peek() as global::app.@event.on.@this;
            reached = await hop.Start(reached, Context);
            if (reached.IsInitialized && reached.Success) continue;
            if (on != null) return Context.Error(NoEvent(path.Text, on));
            await __action.Warn(new global::app.error.Error(
                $"on.event: '{path.Text}' reaches nothing at '{hop.Text}' at build time — it binds only if that exists when the step runs",
                "EventUnreached", 404), Context);
            break;
        }
        return Context.Ok();
    }

    // A path whose last name is no event: it says the item's events, and that before/after is the When.
    private static global::app.error.Error NoEvent(string path, global::app.@event.on.@this on)
        => new global::app.error.ActionError(
            $"on.event: '{path}' names no event — an item's events are {string.Join(", ", on.Names)}; before or after one " +
            "is When (Event=%!app.type.step.on.start%, When=before)", "EventNotFound", 404);

    public async Task<data.@this<global::app.@event.binding.@this>> Start()
    {
        // The path navigates event ← its on ← the item. An item nothing is bound on answers the shared empty
        // events, which don't know their item: the item is the navigation's, and it binds on its own events.
        var reached = await Event.Follow(Context);
        if (reached.Peek() is not global::app.@event.@this named || reached.Parent?.Parent?.Peek() is not global::app.type.item.@this item)
            return Context.Error<global::app.@event.binding.@this>(NoEvent(Event.Peek()?.ToString() ?? Event.Name, global::app.@event.on.@this.Empty));

        var own = item.Own();
        var @event = own[named.Name]!;
        var call = (await Action.Value())!;
        global::app.@event.binding.Scope scope = await Scope.Value();
        var binding = own.Bind(named.Name, await When.Value(),
            side => new global::app.@event.binding.action.@this(side, @event, call, Context.Actor!, scope));
        return Context.Ok<global::app.@event.binding.@this>(binding);
    }
}
