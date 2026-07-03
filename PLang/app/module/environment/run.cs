using app.error;
using app.variable;

namespace app.module.environment;

/// <summary>
/// Unified run action — runs a GoalCall, Step, or Action.
/// Actor switching is handled by the source generator.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial data.@this<GoalCall>? GoalName { get; init; }
    public partial data.@this<Step>? Step { get; init; }
    public partial data.@this<global::app.goal.steps.step.actions.action.@this>? Action { get; init; }
    public partial data.@this<actor.@this>? Actor { get; init; }

    public async Task<data.@this> Run()
    {
        // Polymorphic: forwarded result type depends on the dispatched target.
        var goalName = GoalName == null ? null : await GoalName.Value();
        if (goalName != null)
            return await Context.App.RunGoalAsync(goalName, Context);

        var step = Step == null ? null : await Step.Value();
        if (step != null)
            return await step.RunAsync(Context);

        var action = Action == null ? null : await Action.Value();
        if (action != null)
            return await action.RunAsync(Context);

        return Context.Error(new ActionError(
            "run requires a GoalCall, Step, or Action", "MissingInput", 400));
    }
}
