namespace app.Errors;

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
/// resolves to no goal in the live <c>App.Goals</c> registry (file moved/deleted
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
