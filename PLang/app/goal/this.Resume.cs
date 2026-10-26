namespace app.goal;

/// <summary>
/// The rest of a goal's run, for a resumed call stack: its steps from <c>stepIdx</c> on, each starting as any step
/// does. Pushes no goal frame — the goal's own frame came back with the stack it was saved in.
/// </summary>
public partial class @this
{
    public async Task<data.@this> Resume(actor.context.@this context, int stepIdx)
    {
        data.@this result = context.Ok();
        for (int i = stepIdx; i < Step.Count; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                return context.Error(new error.@this("Operation was cancelled", "Cancelled", 499));

            result = await Step[i].Start(context);
            if (result.ShouldExit()) return result;
        }
        return result;
    }
}
