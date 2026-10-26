namespace app.error;

/// <summary>
/// Hard referent-integrity error raised when a stored frame's step or action index lands outside the goal it was
/// found by — the goal changed between the save and the start.
/// </summary>
public sealed class CallbackGoalNotFound : AppException
{
    public string GoalPrPath { get; }
    public CallbackGoalNotFound(string goalPrPath)
        : base($"Callback frame's goal not found in live registry: '{goalPrPath}'.", "CallbackGoalNotFound", 404)
    {
        GoalPrPath = goalPrPath;
    }
}

/// <summary>
/// Hard referent-integrity error raised when the action at a captured frame's (step, action) index
/// is no longer the action the frame captured. The goal hash covers the goal's name and step TEXT,
/// not the compiled actions — a rebuild with unchanged prose can compile a step to different actions,
/// so the position lands on another action with the same hash. The captured module/name is the check
/// the hash can't make.
/// </summary>
public sealed class CallbackActionMismatch : AppException
{
    public CallbackActionMismatch(string goal, int stepIndex, int actionIndex, string captured, string live)
        : base($"Callback frame at '{goal}' step {stepIndex} action {actionIndex}: captured action '{captured}', live action '{live}'.",
            "CallbackActionMismatch", 409)
    { }
}
