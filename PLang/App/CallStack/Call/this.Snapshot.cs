namespace App.CallStack.Call;

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
    /// </summary>
    public void Capture(global::App.Snapshot.@this s)
    {
        var step = Action.Step;
        var goal = step?.Goal;
        s.Write("goalPrPath",  goal?.PrPath ?? "");
        s.Write("goalHash",    goal?.Hash   ?? "");
        s.Write("stepIndex",   step?.Index  ?? -1);
        s.Write("actionIndex", step?.Actions != null
            ? IndexOfAction(step.Actions, this.Action)
            : -1);
        s.Write("actionModule", Action.Module);
        s.Write("actionName",   Action.ActionName);
        s.Write("id",           Id);
    }

    private static int IndexOfAction(
        global::App.Goals.Goal.Steps.Step.Actions.@this actions,
        global::App.Goals.Goal.Steps.Step.Actions.Action.@this needle)
    {
        for (int i = 0; i < actions.Count; i++)
            if (ReferenceEquals(actions[i], needle))
                return i;
        return -1;
    }
}
