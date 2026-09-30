namespace app.callstack.call;

public sealed partial class @this
{
    /// <summary>
    /// Captures this Call's positional triple plus identity for snapshot. Wire shape:
    ///  - GoalPrPath  : stable identity for live-registry lookup on Restore
    ///  - GoalHash    : SHA-256 of name + step prose; mismatch on resume = hard error
    ///  - StepIndex   : Step.Index inside Goal.Step
    ///  - ActionIndex : index of this Action inside its Step.Action
    ///  - ActionModule, ActionName : the action at the position; restore verifies the live action is
    ///    still it (the hash covers step text, not compiled actions). Action lookup is by index
    ///  - Id          : the Call's short hex Id; preserved for log correlation
    ///
    /// Excludes: timing tier (StartedAt/CompletedAt), Diffs, in-flight network state,
    /// Items bag, Tags. Those are runtime-only audit per the architect's drop bucket.
    ///
    /// <para>Returns whether this frame is a resumable re-entry point (<c>IsResumable</c>): a goal's or a
    /// step's frame, or an action composed in C#, carries no position to resume from and writes nothing.</para>
    /// </summary>
    internal bool Capture(global::app.snapshot.@this s)
    {
        if (Place < 0) return false;
        // Name is the goal's identity for Restore: a v0.2 .pr holds many goals
        // sharing one PrPath (the file), so PrPath alone can't pick the right one.
        s.Write("goalName",    Goal?.Name   ?? "");
        s.Write("goalPrPath",  Goal?.PrPath?.ToString() ?? "");
        s.Write("goalHash",    Goal?.Hash   ?? "");
        s.Write("stepIndex",   Step!.Index);
        s.Write("actionIndex", Place);
        // The module's NAME, not the element: an element is a live graph node whose Actions lead
        // back to their Module. Restore checks the live action at the position against these.
        s.Write("actionModule", Action!.Module.Name);
        s.Write("actionName",   Action.Name);
        s.Write("id",           _id);
        return true;
    }
}
