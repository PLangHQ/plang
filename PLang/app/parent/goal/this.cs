namespace app.parent.goal;

/// <summary>
/// A goal of the app that started this plang — <c>%!app.parent.goal["Claude"]%</c>: its start sends the call up the link
/// to the parent, with the parameters its call gives it, and what comes back is its result. The goal is the parent's, so
/// its events are the parent's and run there: this app's "each goal" doesn't count a call that goes up, while the
/// <c>goal.call</c> action that makes it is this app's, and what is bound on actions runs around it.
/// </summary>
public sealed class @this : global::app.goal.@this
{
    private readonly global::app.parent.@this _parent;

    /// <summary>The goal <paramref name="name"/> of <paramref name="parent"/>.</summary>
    internal @this(string name, global::app.parent.@this parent, global::app.type.list.@this types) : base(types)
    {
        Name = name;
        _parent = parent;
    }

    /// <summary>Up the link with <paramref name="parameters"/>: the parent runs its goal, and its answer is this one's.</summary>
    public override Task<global::app.data.@this> Start(global::app.actor.context.@this context, IEnumerable<global::app.data.@this>? parameters)
        => _parent.Run(Name, parameters ?? [], context);
}
