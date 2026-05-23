using app;
using app.actor.context;
using app.variables;

namespace app.modules.goal;

/// <summary>
/// Calls a named goal, optionally on a different actor.
/// Parameters are injected into the target goal's context by the GoalCall resolver.
/// </summary>
[Action("call")]
public partial class Call : IContext
{
    public partial data.@this<GoalCall> GoalName { get; init; }

    /// <summary>
    /// Target actor to run the goal on. If null, runs on the current context.
    /// </summary>
    public partial data.@this<actor.@this>? Actor { get; init; }

    public async Task<data.@this> Run()
    {
        var goalCall = GoalName.Value!;
        // Stamp THIS action as the anchor so GetGoalAsync can navigate step → goal → sub-goals.
        // Using __action (source-generator self-reference) instead of the step's first action
        // means nested goal.call instances — e.g. inside error.handle's Actions chain —
        // navigate from themselves, not from the outer action that owns the step.
        if (goalCall.Action == null)
            goalCall.Action = __action;
        var execContext = Actor?.Value?.Context ?? Context;
        return await Context.App!.RunGoalAsync(goalCall, execContext);
    }
}
