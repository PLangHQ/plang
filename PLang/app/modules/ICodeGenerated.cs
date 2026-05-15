using app.Actor.Context;
using app.Errors;
using ActionType = app.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace app.modules;

/// <summary>
/// Implemented by generated partial handler classes to support lazy parameter resolution.
/// All handlers must implement this interface — App requires it (no fallback path).
/// </summary>
public interface ICodeGenerated
{
    Task<Data.@this> ExecuteAsync(ActionType action, Actor.Context.@this context);

    /// <summary>
    /// Per-property snapshot of pr-side and final-resolved values.
    /// Called by App.Run from the catch block so the resulting Error carries
    /// enough context to diagnose "param X arrived as Y" without re-running.
    /// Default (no parameter properties) returns an empty list.
    /// </summary>
    List<ParamSnapshot> SnapshotParams() => new();
}
