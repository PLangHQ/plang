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
    /// Build-time hint: the event's path is walked hop by hop, and one that reaches nothing surfaces a
    /// {action, message} warning on Channel("builder") naming that hop. Never a refusal — the item may only
    /// exist at run (a channel a step before it creates); a path that reaches a value that is not an event is
    /// refused by the slot's own type.
    /// </summary>
    public async Task<data.@this> Build()
    {
        if (__action?["Event"]?.Value?.Variable is not [{ } path, ..]) return Context.Ok();

        data.@this? reached = null;
        foreach (var hop in path.Code.Items())
        {
            reached = await hop.Start(reached, Context);
            if (reached.IsInitialized && reached.Success) continue;
            var warning = new global::app.type.item.dict.@this()
                .Set("action", $"{__action.Module}.{__action.Name}")
                .Set("message", $"on.event: '{path.Text}' reaches nothing at '{hop.Text}' at build time — it binds only if that exists when the step runs");
            if (Context.Actor.Channel.Get("builder") is { } builder) await builder.WriteAsync(Context.Ok(warning));
            break;
        }
        return Context.Ok();
    }

    public async Task<data.@this<global::app.@event.binding.@this>> Start()
    {
        // The path navigates event ← its on ← the item. An item nothing is bound on answers the shared empty
        // events, which don't know their item: the item is the navigation's, and it binds on its own events.
        var reached = await Event.Follow(Context);
        if (reached.Peek() is not global::app.@event.@this named || reached.Parent?.Parent?.Peek() is not global::app.type.item.@this item)
            return Context.Error<global::app.@event.binding.@this>(new global::app.error.ActionError(
                "on.event binds on an item's event, reached by its path — e.g. %!app.type.goal.on.start%", "EventNotFound", 404));

        var own = item.Own();
        var @event = own[named.Name]!;
        var call = (await Action.Value())!;
        global::app.@event.binding.Scope scope = await Scope.Value();
        var binding = own.Bind(named.Name, await When.Value(),
            side => new global::app.@event.binding.action.@this(side, @event, call, Context.Actor!, scope));
        return Context.Ok<global::app.@event.binding.@this>(binding);
    }
}
