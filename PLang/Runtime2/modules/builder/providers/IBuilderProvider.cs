using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.Engine.Utility;
using Goal = PLang.Runtime2.Engine.Goals.Goal.@this;
using Actions = PLang.Runtime2.Engine.Goals.Goal.Steps.Step.Actions.@this;

namespace PLang.Runtime2.modules.builder.providers;

/// <summary>
/// Builder provider interface. Owns all builder logic — actions are thin delegation.
/// Swappable via engine.Providers.
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
