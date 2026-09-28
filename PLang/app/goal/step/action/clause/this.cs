namespace app.goal.step.action.clause;

/// <summary>
/// A clause of the action before it — <c>on.error</c>, <c>on.cache</c>, <c>on.timeout</c> (its handler is an
/// <see cref="global::app.module.IClause"/>). Written after that action in its step's code, as its sibling; bound
/// on that action's own events when the program is read (<see cref="Bind"/>), and never started as a step — so it
/// neither runs nor answers <c>%!data%</c>. It fires for every actor running the program.
/// </summary>
[global::app.Attributes.PlangType("clause")]
public class @this : global::app.goal.step.action.@this
{
    /// <summary>A clause IS a distinct plang type (the role is the type): its wire shape is action's, its identity
    /// its own.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    internal override bool IsClause => true;

    /// <summary>A program action of this clause's kind, in <paramref name="step"/>.</summary>
    internal override global::app.goal.step.action.@this Program(global::app.goal.step.@this? step)
        => new @this { Module = Module, Name = Name, Step = step, Synthetic = false };

    /// <summary>Binds this clause on <paramref name="action"/>'s own events — its handler knows which, and on
    /// which side.</summary>
    internal override void Bind(global::app.goal.step.action.@this action)
        => ((global::app.module.IClause)Module.Create(Name, Module.App.System.Context)!).Bind(this, action);
}
