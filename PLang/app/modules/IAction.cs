namespace app.modules;

/// <summary>
/// Capability interface — gives the handler access to the Action that triggered it.
/// The source generator wires Action = action in ExecuteAsync.
/// Navigate: Action.Step.Goal for the full chain.
/// </summary>
public interface IAction
{
    app.Goals.Goal.Steps.Step.Actions.Action.@this Action { get; set; }
}
