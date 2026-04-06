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
    Task<Data> Actions(GetActions action);
    Data Types(types action);
    Task<Data> Goals(goals action);
    Task<Data> GoalsSave(goalsSave action);
    Task<Data> Validate(validate action);
    Data Merge(merge action);
    Task<Data> App(app action);
    Task<Data> AppSave(appSave action);
}
