namespace app.@event.binding.inherited;

/// <summary>
/// A maker's binding a sub app took in at its birth, on the sub app's own list: it runs the maker's, for this app's actor
/// in the maker binding's role (the sub app's user for the maker's user). It is the maker's policy, so the sub app
/// neither saves it nor takes it off, and shows nothing of the maker's.
/// </summary>
public sealed class @this : global::app.@event.binding.@this
{
    // the maker's binding this one runs
    private readonly global::app.@event.binding.@this _binding;

    /// <summary><paramref name="binding"/>, the maker's, taken in on <paramref name="side"/> of <paramref name="app"/>'s
    /// own list.</summary>
    internal @this(global::app.@event.binding.list.@this side, global::app.@event.binding.@this binding, global::app.@this app)
        : base(side, binding, binding.Actor.Role.In(app.actor.list))
        => _binding = binding;

    // the maker's: the action that made the binding it runs
    internal override global::app.goal.step.action.@this? action => _binding.action;

    // it runs the maker's: its call is found in the maker's app (the action binding's own Handle)
    private protected override System.Threading.Tasks.Task<global::app.data.@this> Handle(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
        => _binding.Start(item, result, context);

    /// <summary>The maker's policy: a sub app never takes it off.</summary>
    public override void Remove()
        => throw new global::app.error.AppException(new global::app.error.@this(
            "this binding is the app's maker's: a sub app can't take it off", "BindingNotYours", 403));
}
