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
    /// </summary>
    public void Capture(global::app.snapshot.@this s)
    {
        var step = Action.Step;
        var goal = step?.Goal;
        s.Write("goalPrPath",  goal?.PrPath?.ToString() ?? "");
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
        global::app.goal.steps.step.actions.@this actions,
        global::app.goal.steps.step.actions.action.@this needle)
    {
        for (int i = 0; i < actions.Count; i++)
            if (ReferenceEquals(actions[i], needle))
                return i;
        return -1;
    }

    /// <summary>
    /// Serializes one captured frame onto its own wire cursor. The Call owns the
    /// frame's key set (the same keys <see cref="Capture"/> writes) — CallStack
    /// owns only the list, not each frame's shape. Naming each scalar's concrete
    /// type here is what keeps <c>stepIndex</c>/<c>actionIndex</c> coming back as
    /// <c>int</c> (not <c>long</c>), which <see cref="callstack.@this.Restore"/>'s
    /// <c>Read&lt;int&gt;</c> depends on.
    /// </summary>
    public static void WriteFrame(global::app.snapshot.@this frame, global::app.snapshot.Io io)
    {
        io.Put("goalPrPath",   frame.Read<string>("goalPrPath") ?? "");
        io.Put("goalHash",     frame.Read<string>("goalHash")   ?? "");
        io.Put("stepIndex",    frame.Read<int>("stepIndex"));
        io.Put("actionIndex",  frame.Read<int>("actionIndex"));
        io.Put("actionModule", frame.Read<string>("actionModule") ?? "");
        io.Put("actionName",   frame.Read<string>("actionName") ?? "");
        io.Put("id",           frame.Read<string>("id") ?? "");
    }

    /// <summary>Rehydrates one frame's scalars into a section, preserving the int typing.</summary>
    public static void ReadFrame(global::app.snapshot.Io io, global::app.snapshot.@this frame)
    {
        frame.Write("goalPrPath",   io.Get<string>("goalPrPath") ?? "");
        frame.Write("goalHash",     io.Get<string>("goalHash")   ?? "");
        frame.Write("stepIndex",    io.Get<int>("stepIndex"));
        frame.Write("actionIndex",  io.Get<int>("actionIndex"));
        frame.Write("actionModule", io.Get<string>("actionModule") ?? "");
        frame.Write("actionName",   io.Get<string>("actionName") ?? "");
        frame.Write("id",           io.Get<string>("id") ?? "");
    }
}
