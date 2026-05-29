using app.data;

namespace app.snapshot;

/// <summary>
/// Resume — the only path the channel/host uses to reconstitute a suspended
/// goal. Calls App.Restore to populate Variables/Errors/CallStack/etc., then
/// walks the restored CallStack chain recursively so each nested goal in the
/// chain re-enters at its captured (StepIndex, ActionIndex).
///
/// The recursion is acknowledged-clunky and tracked in todos.md for revisit;
/// it works as designed: bottom frame re-runs the suspended action via
/// Goal.RunFrom; parent frames push their call frame, recurse into the child,
/// then continue from ActionIndex+1 (the action after the `call SubGoal`).
/// </summary>
public sealed partial class @this
{
    public async Task<data.@this> Resume(actor.context.@this context)
    {
        context.App.Restore(this, context);
        var chain = context.App.CallStack.RestoredChain;
        if (chain == null || chain.Count == 0)
            return data.@this.FromError(new global::app.error.ServiceError(
                "Resume has no frames after Restore", "NoPosition", 400));
        return await ResumeChain(chain, 0, context);
    }

    private static async Task<data.@this> ResumeChain(
        IReadOnlyList<callstack.call.Position> chain, int idx, actor.context.@this ctx)
    {
        var frame = chain[idx];

        // Bottom: re-enter the goal at the suspended (StepIndex, ActionIndex).
        if (idx == chain.Count - 1)
            return await frame.Goal.RunFrom(ctx, frame.StepIndex, frame.ActionIndex);

        // Parent: its action is a "call SubGoal" mid-flight. Push so children
        // see it as caller, recurse into the sub-goal, then continue from
        // ActionIndex+1 (the action after the call).
        await using var callFrame = ctx.App.CallStack.Push(frame.Action, ctx.Variables);

        var subResult = await ResumeChain(chain, idx + 1, ctx);
        if (subResult.ShouldExit()) return subResult;

        return await frame.Goal.RunFrom(ctx, frame.StepIndex, frame.ActionIndex + 1);
    }
}
