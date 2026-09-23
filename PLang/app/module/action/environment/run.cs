using app.error;
using app.variable;

namespace app.module.action.environment;

/// <summary>
/// Unified run action — runs a goal call, Step, or Action, on the named actor when one is given.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    /// <summary>The goal to run — a <c>goal.call</c> action, run as itself.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? Goal { get; init; }
    public partial data.@this<Step>? Step { get; init; }
    public partial data.@this<global::app.goal.step.action.@this>? Action { get; init; }
    /// <summary>The actor to run on, by name. If null, runs on the current context.</summary>
    public partial data.@this<global::app.type.item.choice.@this<actor.Name>>? Actor { get; init; }

    public async Task<data.@this> Run()
    {
        var named = Actor == null ? null : await Actor.Value();
        var runContext = named == null ? Context : Context.App.Actor[named].Context;

        // Polymorphic: forwarded result type depends on the dispatched target.
        var call = Goal == null ? null : await Goal.Value();
        if (call != null)
            return await call.Run(runContext);

        var step = Step == null ? null : await Step.Value();
        if (step != null)
            return await step.Run(runContext);

        var action = Action == null ? null : await Action.Value();
        if (action != null)
            return await action.Run(runContext);

        return Context.Error(new ActionError(
            "run requires a Goal, Step, or Action", "MissingInput", 400));
    }
}
