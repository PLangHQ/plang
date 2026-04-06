using App.Context;
using App.Variables;
using ActionType = App.Goals.Goal.Steps.Step.Actions.Action.@this;
using EngineType = App.@this;

namespace App.modules;

/// <summary>
/// Implemented by generated partial handler classes to support lazy parameter resolution.
/// All handlers must implement this interface — Engine requires it (no fallback path).
/// </summary>
public interface ICodeGenerated
{
    Task<Data> ExecuteAsync(ActionType action, EngineType engine, PLangContext context);
}
