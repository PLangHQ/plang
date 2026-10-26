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
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or OperationCanceledException))
        {
            var typeName = ex.GetType().Name;
            var key = typeName == nameof(Exception)
                ? "StepError"
                : (typeName.EndsWith("Exception", StringComparison.Ordinal)
                    ? typeName[..^"Exception".Length]
                    : typeName);
            result = context.Error(new error.ServiceError(
                ex.Message, key, 400) { Exception = ex });
        }

        return result;
    }
}
