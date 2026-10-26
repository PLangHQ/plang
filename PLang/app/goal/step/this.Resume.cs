using app.data;

namespace app.goal.step;

/// <summary>
/// The rest of a step's run, for a resumed call stack — its actions from <c>fromActionIdx</c> on, in the step's own
/// frame, which came back with the stack it was saved in. No before/after step events fire (the step was already in
/// flight when the run stopped). Mirrors RunAsync's action-loop semantics.
/// </summary>
public partial class @this
{
    public async Task<data.@this> Resume(actor.context.@this context, int fromActionIdx)
    {
        data.@this result = context.Ok();
        if (fromActionIdx < 0 || fromActionIdx >= Code.Count) return result;

        try
        {
            for (int i = fromActionIdx; i < Code.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                result = await Code[i].Start(context);
                if (result.ShouldExit() || result.Handled) break;
            }
        }
        // an exception is answered through the one door from any exception to an error: a carried one whole
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or OperationCanceledException))
        {
            result = context.Error(error.@this.FromException(ex));
        }

        return result;
    }
}
