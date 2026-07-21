using app.variable;
using app.module.action.code;
using app.Utils;
using Goal = app.goal.@this;
using Actions = System.Collections.Generic.List<app.goal.step.action.@this>;

namespace app.module.action.build.code;

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
    Task<data.@this> Fold(fold action);
    Task<data.@this> Validate(validate action);
    Task<data.@this> ValidateStepActions(validateStepActions action);
    Task<data.@this> Merge(merge action);
    Task<data.@this> PromoteGroups(promoteGroups action);
    Task<data.@this> EnrichResponse(enrichResponse action);
    Task<data.@this> Load(load action);
    Task<data.@this> AppSave(appSave action);
}
