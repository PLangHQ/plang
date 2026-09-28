namespace app.module.action.on;

/// <summary>
/// Binds a call on an item's event: <see cref="Action"/> runs <see cref="When"/> (before or after) the
/// <see cref="Event"/> (start, write, read, ask, create, set, remove, load, …) of <see cref="Item"/> — a goal
/// (<c>%!app.goal["/x"]%</c>), every goal or step (<c>%!app.type.goal%</c>, <c>%!app.type.step%</c>), a module or
/// one of its actions (<c>%!app.module.file.read%</c>), a channel (<c>%!app.actor.user.channel.audit%</c>).
/// The call reads <c>%!event%</c>: the event, with <c>%!event!item%</c> (what it fired for) and
/// <c>%!event!result%</c> (the result so far). Bound for this actor unless <see cref="Scope"/> is app.
/// Answers the binding — <c>on.unbind</c> takes it off.
/// </summary>
[Action("event", Cacheable = false)]
public partial class OnEvent : IContext
{
    /// <summary>The item whose event it binds on.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.@this> Item { get; init; }

    /// <summary>Before the event, or after it.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<global::app.@event.When>> When { get; init; }

    /// <summary>The event's name — the item's verb (write, read, ask, …); a goal's, step's or action's is start.</summary>
    [Default("start")]
    public partial data.@this<global::app.type.item.text.@this> Event { get; init; }

    /// <summary>The call run when it fires — a <c>goal.call</c> action, held whole.</summary>
    [IsNotNull]
    public partial data.@this<global::app.goal.step.action.@this> Action { get; init; }

    /// <summary>Whose the binding is: this actor's (it fires while this actor runs), or the app's.</summary>
    [Default(global::app.@event.binding.Scope.actor)]
    public partial data.@this<global::app.type.item.choice.@this<global::app.@event.binding.Scope>> Scope { get; init; }

    public async Task<data.@this<global::app.@event.binding.@this>> Start()
    {
        var item = (await Item.Value())!;
        var name = (await Event.Value())!.ToString();
        var own = item.Own();
        var @event = own[name];
        if (@event == null)
            return Context.Error<global::app.@event.binding.@this>(new global::app.error.ActionError(
                $"{item.Type.Name} has no event '{name}'", "EventNotFound", 404));

        global::app.@event.When when = await When.Value();
        global::app.@event.binding.Scope scope = await Scope.Value();
        var call = (await Action.Value())!;
        var binding = own.Bind(name, when, async (fired, result, context) =>
        {
            // %!event% is the running event; what it fired for and the result so far (its value — a property
            // holds a value, never a Data; a failed result is %!error%) are this firing's own
            var running = new global::app.data.@this("!event", @event, context: context);
            running.Properties.Set("item", fired);
            running.Properties.Set("result", result.Peek());
            await context.Variable.Set("!event", running);
            return await call.Start(context);
        }, Context.Actor!, scope);
        return Context.Ok<global::app.@event.binding.@this>(binding);
    }
}
