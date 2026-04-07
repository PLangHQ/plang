using App.Actor.Context;
using App.Variables;
using ActionType = App.Goals.Goal.Steps.Step.Actions.Action.@this;

namespace App.modules;

/// <summary>
/// Implemented by generated partial handler classes to support lazy parameter resolution.
/// All handlers must implement this interface — App requires it (no fallback path).
/// </summary>
public interface ICodeGenerated
{
    /// <summary>Parameters from .pr action — set by App.Run before ExecuteAsync.</summary>
    List<Data.@this>? PrParameters { get; set; }

    /// <summary>Default values from .pr action — set by App.Run before ExecuteAsync.</summary>
    List<Data.@this>? PrDefaults { get; set; }

    /// <summary>The .pr action this handler was dispatched from. Null for C# composition (RunAction).</summary>
    ActionType? PrAction { get; set; }

    Task<Data.@this> ExecuteAsync(App.@this app, Actor.Context.@this context);
}
