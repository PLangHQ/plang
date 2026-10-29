namespace app.@event.inherited;

/// <summary>
/// A maker's event a sub app's actor took in when its events first loaded, bound on what the maker's was: it runs the maker's, for this
/// app's actor in the maker event's role (the sub app's user for the maker's user), as the event running here. It is the
/// maker's policy, so the sub app neither saves it nor takes it off, and shows nothing of the maker's.
/// </summary>
public sealed class @this : global::app.@event.@this
{
    // the maker's event this one runs
    private readonly global::app.@event.@this _event;

    /// <summary><paramref name="event"/>, the maker's, taken in by <paramref name="app"/>'s actor of its role.</summary>
    internal @this(global::app.@event.@this @event, global::app.@this app)
        : base(@event.Item, @event.Name, @event.When, @event.Actor.Role.In(app.actor.list))
        => _event = @event;

    // the maker's: the action that made the event it runs
    internal override global::app.goal.step.action.@this? action => _event.action;

    // it runs the maker's: its call is found in the maker's app (the call event's own Start)
    internal override System.Threading.Tasks.Task<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
        => _event.Start(item, result, context);

    /// <summary>The maker's policy: a sub app never takes it off.</summary>
    public override void Remove()
        => throw new global::app.error.AppException(new global::app.error.@this(
            "this event is the app's maker's: a sub app can't take it off", "EventNotYours", 403));
}
