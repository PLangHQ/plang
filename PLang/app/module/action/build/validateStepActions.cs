using app.variable;
using app.module.action.build.code;

namespace app.module.action.build;

/// <summary>
/// Pre-compile pass over the planner's action set for one step. Drops any planner
/// suggestion that doesn't exist in the runtime catalog (catches hallucinations
/// before they reach the compiler), then appends any explicit <c>module.action</c>
/// tokens found in the step text — gated by the same catalog existence check so
/// false-positive dotpaths (<c>%goal.Name%</c>, <c>result.actions</c>) get ignored.
///
/// Append-only on the input. The planner's order is preserved; new entries from
/// step text are appended at the end.
///
/// Why: the v3 builder once wrote a corrupted build.pr where step text said
/// <c>builder.goals</c> but the LLM compiled <c>builder.goalsSave</c>. The
/// planner had suggested <c>goalsSave</c>; the compiler had no catalog row for
/// the actually-mentioned <c>builder.goals</c> in front of it, so it picked the
/// closest neighbour. With this action wired in, the explicit <c>module.action</c>
/// from the step text always lands in the compiler's catalog detail.
/// </summary>
[Action("validateStepActions")]
public partial class validateStepActions : IContext
{
    public partial data.@this<global::app.type.clr.@this<global::app.goal.steps.step.@this>> Step { get; init; }
    public partial data.@this<global::app.type.item.list.@this> Actions { get; init; }

    [Code]
    public partial IBuilder Builder { get; }

    public Task<data.@this> Run() => Builder.ValidateStepActions(this);
}
