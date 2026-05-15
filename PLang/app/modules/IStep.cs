namespace app.modules;

/// <summary>
/// Capability interface — gives the handler access to the Step that contains this action.
/// The source generator wires Step = action.Step in ExecuteAsync.
/// Navigate: Step.Goal for the parent goal, Step.Actions for sibling actions.
/// </summary>
public interface IStep
{
    app.goals.goal.steps.step.@this Step { get; set; }
}
