using app.Variables;
using app.Code;
using app.Utils;
using Goal = app.Goals.Goal.@this;
using Actions = app.Goals.Goal.Steps.Step.Actions.@this;

namespace app.modules.builder.code;

/// <summary>
/// Builder provider interface. Owns all builder logic — actions are thin delegation.
/// Swappable via app.Code.
/// </summary>
public interface IBuilder : ICode
{
    Task<Data.@this> Actions(GetActions action);
    Data.@this Types(types action);
    Task<Data.@this> Goals(goals action);
    Task<Data.@this> GoalsSave(goalsSave action);
    Task<Data.@this> Validate(validate action);
    Data.@this Merge(merge action);
    Task<Data.@this> PromoteGroups(promoteGroups action);
    Data.@this EnrichResponse(enrichResponse action);
    Task<Data.@this> Load(load action);
    Task<Data.@this> AppSave(appSave action);
}
