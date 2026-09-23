using app.error;
using app.variable;

namespace app.module.action.environment;

/// <summary>
/// Unified run action — runs a goal call, Step, or Action.
/// Actor switching is handled by the source generator.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    /// <summary>The goal to run — a <c>goal.call</c> action, run as itself.</summary>
    public partial data.@this<global::app.goal.step.action.@this>? Goal { get; init; }
    public partial data.@this<Step>? Step { get; init; }
    public partial data.@this<global::app.goal.step.action.@this>? Action { get; init; }
    public partial data.@this<actor.@this>? Actor { get; init; }

    public async Task<data.@this> Run()
    {
        // Polymorphic: forwarded result type depends on the dispatched target.
        var call = Goal == null ? null : await Goal.Value();
        if (call != null)
            return await call.Run(Context);

        var step = Step == null ? null : await Step.Value();
        if (step != null)
            return await step.Run(Context);

        var action = Action == null ? null : await Action.Value();
        if (action != null)
            return await action.Run(Context);

        return Context.Error(new ActionError(
            "run requires a Goal, Step, or Action", "MissingInput", 400));
    }
}
