namespace app.module.on;

/// <summary>
/// Binds a call on a moment: <see cref="Action"/> runs <see cref="When"/> (before or after) <see cref="Event"/> — an
/// item's moment, reached by its path: <c>%!app.type.goal.on.start%</c> (each goal starting),
/// <c>%!app.module.file.read.on.start%</c> (each file.read), <c>%!channels.audit.on.write%</c> (a channel writing).
/// The call reads <c>%!event%</c>: the event, with <c>%!event!item%</c> (what it ran for) and <c>%!event!data%</c> (the
/// value so far; a failure is <c>%!error%</c>) — its own, for as long as it runs. Bound for this actor: a goal run as
/// another actor never runs it. Answers the event — <c>on.unbind</c> takes it off.
/// </summary>
[Action("event", Cacheable = false)]
public partial class OnEvent : IContext
{
    /// <summary>The moment it binds on — an item's, by its path (<c>…on.start</c>, <c>…on.write</c>).</summary>
    [IsNotNull]
    public partial data.@this<global::app.@event.on.@this> Event { get; init; }

    /// <summary>Before the moment, or after it.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<global::app.@event.When>> When { get; init; }

    /// <summary>The call run when it comes — a <c>goal.call</c> action, held whole.</summary>
    [IsNotNull]
    public partial data.@this<global::app.goal.step.action.@this> Action { get; init; }

    /// <summary>
    /// Build-time check: the moment's path is walked hop by hop. A name the item's moments refuse (<c>…on.before</c>)
    /// is the answer — every item has the same moments, so it can never bind. Any other hop that reaches nothing
    /// surfaces a warning on Channel("builder") naming it — the item may only exist at run (a channel a step before
    /// it creates).
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
            if (on != null) return reached;
            await __action.Warn(new global::app.error.@this(
                $"on.event: '{path.Text}' reaches nothing at '{hop.Text}' at build time — it binds only if that exists when the step runs",
                "EventUnreached", 404), Context);
            break;
        }
        return Context.Ok();
    }

    public async Task<data.@this<global::app.@event.@this>> Start()
    {
        // The path navigates to an item's moment, which knows its item. A name the moments refuse is that refusal; an
        // item that isn't there is unreached; a path ending at something other than a moment names no event.
        var path = Event.Peek()?.ToString() ?? Event.Name;
        var reached = await Event.Follow(Context);
        if (!reached.Success) return Context.Error<global::app.@event.@this>(reached.Error!);
        if (!reached.IsInitialized)
            return Context.Error<global::app.@event.@this>(new global::app.error.@this(
                $"on.event: '{path}' reaches nothing — the item isn't there to bind on", "EventUnreached", 404));
        if (reached.Peek() is not global::app.@event.on.@this { item: { } item } moment)
            return Context.Error<global::app.@event.@this>(global::app.@event.on.empty.Of(typeof(global::app.type.item.@this)).NoEvent(path));

        // each carrier is opened through its door: the first that doesn't resolve (a %variable% holding nothing, a
        // name that is none of the choice's) is the answer, and nothing is held for it. Bound on this actor's events,
        // never on the item: it runs while this actor runs (and in the sub apps it makes after) and goes with it. The
        // event holds this action, whose Action it runs when its moment comes, and the actor that bound it.
        return data.@this<global::app.@event.@this>.From(await When.Use(when => Action.Use(async _ =>
        {
            var @event = await Context.Actor!.@event.Add(new global::app.@event.call.@this(item, moment.Name, when.Value, __action!, Context.Actor!));
            return (data.@this)Context.Ok<global::app.@event.@this>(@event);
        })));
    }
}
