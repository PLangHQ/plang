using app.data;

namespace app.goal;

/// <summary>
/// Continuation entry for Snapshot resume. Picks up at (stepIdx, actionIdx),
/// finishes that step's remaining actions, then continues through the rest
/// of the goal's steps. Does NOT push a goal frame — Snapshot.Resume already
/// restored the CallStack chain before dispatching here.
/// </summary>
public partial class @this
{
    public async Task<data.@this> Resume(actor.context.@this context, int stepIdx, int actionIdx)
    {
        if (stepIdx < 0 || stepIdx >= Steps.Count)
            return context.Error(new error.ServiceError(
                $"Resume: stepIdx {stepIdx} out of range [0, {Steps.Count})",
                "InvalidPosition", 400));

        var step = Steps[stepIdx];
        step.Goal ??= this;

        var result = await step.Resume(context, actionIdx);
        if (result.ShouldExit()) return result;

        for (int i = stepIdx + 1; i < Steps.Count; i++)
        {
            var s = Steps[i];
            s.Goal ??= this;

            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new error.Error("Operation was cancelled", "Cancelled", 499));

            result = await s.RunAsync(context);
            if (result.ShouldExit()) return result;
        }

        return result;
    }
}
