using app.error;

namespace app.module.action.environment;

/// <summary>
/// Unified start action — starts a goal call, Step, or Action, on the named actor when one is given.
/// </summary>
[Action("start")]
public partial class start : IContext
{
    /// <summary>The goal to start — a <c>goal.call</c> action, started as itself.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? Goal { get; init; }
    public partial data.@this<Step>? Step { get; init; }
    public partial data.@this<global::app.goal.step.action.@this>? Action { get; init; }
    /// <summary>The actor to start on, by name. If null, starts on the current context.</summary>
    public partial data.@this<global::app.type.item.choice.@this<actor.Name>>? Actor { get; init; }

    public async Task<data.@this> Start()
    {
        var named = Actor == null ? null : await Actor.Value();
        var runContext = named == null ? Context : (await (await Context.App.actor.Get(named.ToString()!)).Value())!.Context;

        // Polymorphic: forwarded result type depends on the dispatched target.
        var call = Goal == null ? null : await Goal.Value();
        if (call != null)
            return await call.Start(runContext);

        var step = Step == null ? null : await Step.Value();
        if (step != null)
            return await step.Start(runContext);

        var action = Action == null ? null : await Action.Value();
        if (action != null)
            return await action.Start(runContext);

        return Context.Error(new ActionError(
            "start requires a Goal, Step, or Action", "MissingInput", 400));
    }
}
