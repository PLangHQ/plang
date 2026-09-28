namespace app.goal.step.action.clause;

/// <summary>
/// A clause of the action before it — <c>on.error</c>, <c>on.cache</c>, <c>on.timeout</c> (its handler is an
/// <see cref="global::app.module.IClause"/>). Written after that action in its step's code, as its sibling; attached
/// to that action's own events when the program is read (<see cref="Attach"/>), and never started as a step — so it
/// neither runs nor answers <c>%!data%</c>. It fires for every actor running the program.
/// </summary>
[global::app.Attributes.PlangType("clause")]
public class @this : global::app.goal.step.action.@this
{
    /// <summary>A clause IS a distinct plang type (the role is the type): its wire shape is action's, its identity
    /// its own.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    /// <summary>A program action of this clause's kind, in <paramref name="step"/>.</summary>
    internal override global::app.goal.step.action.@this Program(global::app.goal.step.@this? step)
        => new @this { Module = Module, Name = Name, Step = step, Synthetic = false };

    /// <summary>Binds this clause on <paramref name="before"/>'s own events — its handler knows which, and on
    /// which side.</summary>
    internal override void Attach(global::app.goal.step.action.@this? before)
    {
        if (before != null)
            ((global::app.module.IClause)Module.Create(Name, Module.App.System.Context)!).Bind(this, before);
    }

    /// <summary>A clause belongs to the step action before it, and so does what follows it.</summary>
    internal override global::app.goal.step.action.@this? Anchor(global::app.goal.step.action.@this? before) => before;

    /// <summary>A clause was attached when the program was read: its turn leaves the chain's result — and
    /// <c>%!data%</c> — as they stand.</summary>
    internal override Task<global::app.data.@this> Follow(global::app.data.@this result, actor.context.@this context)
        => Task.FromResult(result);

    /// <summary>A recovery that runs the very action it is a clause of is a retry, not a recovery.</summary>
    internal override global::app.error.Error? Refuse(global::app.goal.step.action.@this? before)
        => before != null && Held.Any(before.Same)
            ? new global::app.error.Error(
                $"the Recovery of {Module}.{Name} runs {before.Module}.{before.Name}, the action it is " +
                "a clause of — Recovery holds what the step runs on error", "RecoveryIsTheAction", 400)
            : null;

    /// <summary>A clause is shown right after the step's first action — the action it is a clause of (<c>?</c>
    /// while that action isn't known).</summary>
    internal override void Prefill(global::app.goal.step.pick.line.@this line, string call) => line.Insert(call);

    /// <summary>A clause adds nothing to the known code: it binds nothing and answers no <c>%!data%</c>.</summary>
    internal override void Know(List<string> line, string call) { }

    /// <summary>A clause's body is the action before it.</summary>
    internal override string? Nest()
        => $"`{Module.Name}.{Name}` takes no {{ }}: write the action first, then {Module.Name}.{Name} after it — " +
           $"next.action(…); {Module.Name}.{Name}(…)";
}
