namespace app.error;

/// <summary>
/// Hard referent-integrity error raised by <c>App.CallStack.@this.Restore</c> when
/// a captured frame's <c>Goal.Hash</c> differs from the goal currently registered
/// at the same <c>PrPath</c>. Means the goal file was redeployed (prose changed)
/// between callback issue and resume — the bind point is no longer the same code.
/// No silent fallback.
/// </summary>
public sealed class CallbackGoalHashMismatch : System.Exception
{
    public string GoalPrPath { get; }
    public string CapturedHash { get; }
    public string LiveHash { get; }

    public CallbackGoalHashMismatch(string goalPrPath, string capturedHash, string liveHash)
        : base($"Callback frame's goal hash mismatch at '{goalPrPath}': captured={capturedHash}, live={liveHash}.")
    {
        GoalPrPath = goalPrPath;
        CapturedHash = capturedHash;
        LiveHash = liveHash;
    }
}

/// <summary>
/// Hard referent-integrity error raised when a captured frame's <c>GoalPrPath</c>
/// resolves to no goal in the live <c>app.goal</c> registry (file moved/deleted
/// between callback issue and resume). Sibling of <see cref="CallbackGoalHashMismatch"/>;
/// the difference is "goal not found" vs "found but different content."
/// </summary>
public sealed class CallbackGoalNotFound : System.Exception
{
    public string GoalPrPath { get; }
    public CallbackGoalNotFound(string goalPrPath)
        : base($"Callback frame's goal not found in live registry: '{goalPrPath}'.")
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
public sealed class CallbackActionMismatch : System.Exception
{
    public CallbackActionMismatch(string goal, int stepIndex, int actionIndex, string captured, string live)
        : base($"Callback frame at '{goal}' step {stepIndex} action {actionIndex}: captured action '{captured}', live action '{live}'.")
    { }
}

/// <summary>
/// Hard referent-integrity error raised when a captured frame lacks an entry its restore needs — the
/// snapshot is not one this runtime wrote, or it was cut.
/// </summary>
public sealed class CallbackFrameIncomplete : System.Exception
{
    public CallbackFrameIncomplete(string key)
        : base($"Callback frame is missing '{key}' — the snapshot cannot place it.")
    { }
}
