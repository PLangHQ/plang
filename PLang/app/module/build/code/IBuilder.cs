using app.variable;
using app.module.code;
using app.Utils;
using Goal = app.goal.@this;
using Actions = app.goal.steps.step.actions.@this;

namespace app.module.build.code;

/// <summary>
/// Builder provider interface. Owns all builder logic — actions are thin delegation.
/// Swappable via app.Code.
/// </summary>
public interface IBuilder : ICode
{
    Task<data.@this> Actions(GetActions action);
    Task<data.@this> Types(types action);
    Task<data.@this> Goals(goals action);
    Task<data.@this> GoalsSave(goalsSave action);
    Task<data.@this> Validate(validate action);
    Task<data.@this> ValidateStepActions(validateStepActions action);
    data.@this Merge(merge action);
    Task<data.@this> PromoteGroups(promoteGroups action);
    data.@this EnrichResponse(enrichResponse action);
    Task<data.@this> Load(load action);
    Task<data.@this> AppSave(appSave action);
}
