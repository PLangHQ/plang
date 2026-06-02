namespace app.callstack.call;

public sealed partial class @this
{
    /// <summary>
    /// Captures this Call's positional triple plus identity for snapshot. Wire shape:
    ///  - GoalPrPath  : stable identity for live-registry lookup on Restore
    ///  - GoalHash    : SHA-256 of name + step prose; mismatch on resume = hard error
    ///  - StepIndex   : Step.Index inside Goal.Steps
    ///  - ActionIndex : index of this Action inside its Step.Actions
    ///  - ActionModule, ActionName : human-readable position help; Action lookup is by index
    ///  - Id          : the Call's short hex Id; preserved for log correlation
    ///
    /// Excludes: timing tier (StartedAt/CompletedAt), Diffs, in-flight network state,
    /// Items bag, Tags. Those are runtime-only audit per the architect's drop bucket.
    ///
    /// <para>Returns whether this frame is a resumable re-entry point. A goal-enter
    /// frame's synthetic Action isn't in any Step's Actions (actionIndex -1), so it
    /// carries no position to resume from — the Call says so itself; the collection
    /// doesn't inspect the captured shape to decide.</para>
    /// </summary>
    public bool Capture(global::app.snapshot.@this s)
    {
        var step = Action.Step;
        var goal = step?.Goal;
        var actionIndex = step?.Actions != null ? IndexOfAction(step.Actions, this.Action) : -1;
        // Name is the goal's identity for Restore: a v0.2 .pr holds many goals
        // sharing one PrPath (the file), so PrPath alone can't pick the right one.
        s.Write("goalName",    goal?.Name   ?? "");
        s.Write("goalPrPath",  goal?.PrPath?.ToString() ?? "");
        s.Write("goalHash",    goal?.Hash   ?? "");
        s.Write("stepIndex",   step?.Index  ?? -1);
        s.Write("actionIndex", actionIndex);
        s.Write("actionModule", Action.Module);
        s.Write("actionName",   Action.ActionName);
        s.Write("id",           Id);
        return actionIndex >= 0;
    }

    private static int IndexOfAction(
        global::app.goal.steps.step.actions.@this actions,
        global::app.goal.steps.step.actions.action.@this needle)
    {
        for (int i = 0; i < actions.Count; i++)
            if (ReferenceEquals(actions[i], needle))
                return i;
        return -1;
    }

    /// <summary>
    /// Rehydrates one frame's scalars into a section, preserving the int typing
    /// (<c>stepIndex</c>/<c>actionIndex</c> as <c>int</c>, not <c>long</c>) that
    /// <see cref="callstack.@this.Restore"/>'s <c>Read&lt;int&gt;</c> depends on.
    /// </summary>
    public static void ReadFrame(global::app.snapshot.Io io, global::app.snapshot.@this frame)
    {
        frame.Write("goalName",     io.Get<string>("goalName")   ?? "");
        frame.Write("goalPrPath",   io.Get<string>("goalPrPath") ?? "");
        frame.Write("goalHash",     io.Get<string>("goalHash")   ?? "");
        frame.Write("stepIndex",    io.Get<int>("stepIndex"));
        frame.Write("actionIndex",  io.Get<int>("actionIndex"));
        frame.Write("actionModule", io.Get<string>("actionModule") ?? "");
        frame.Write("actionName",   io.Get<string>("actionName") ?? "");
        frame.Write("id",           io.Get<string>("id") ?? "");
    }
}
