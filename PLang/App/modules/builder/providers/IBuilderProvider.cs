using App.Variables;
using App.Providers;
using App.Utils;
using Goal = App.Goals.Goal.@this;
using Actions = App.Goals.Goal.Steps.Step.Actions.@this;

namespace App.modules.builder.providers;

/// <summary>
/// Builder provider interface. Owns all builder logic — actions are thin delegation.
/// Swappable via app.Providers.
/// </summary>
public interface IBuilderProvider : IProvider
{
    Task<Data.@this> Actions(GetActions action);
    Data.@this Types(types action);
    Task<Data.@this> Goals(goals action);
    Task<Data.@this> GoalsSave(goalsSave action);
    Task<Data.@this> Validate(validate action);
    Data.@this Merge(merge action);
    Data.@this PromoteGroups(promoteGroups action);
    Data.@this EnrichResponse(enrichResponse action);
    Task<Data.@this> App(app action);
    Task<Data.@this> AppSave(appSave action);
}
