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
    Task<data.@this> Goals(goals action);
    Task<data.@this> GoalsSave(goalsSave action);
    Task<data.@this> Fold(fold action);   // async in Default — materializes Goal via .Value()
    Task<data.@this> Match(match action);
    Task<data.@this> Pick(pick action);
    Task<data.@this> Load(load action);
    Task<data.@this> AppSave(appSave action);
}
