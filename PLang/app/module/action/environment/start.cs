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

    public Task<data.@this> Start() => Context.App.actor.list.Use(Actor, Context, runner => Run(runner.Context));

    private async Task<data.@this> Run(global::app.actor.context.@this runContext)
    {
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
