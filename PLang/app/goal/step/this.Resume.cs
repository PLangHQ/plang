using app.data;

namespace app.goal.step;

/// <summary>
/// Continuation entry for resume — runs actions starting at <paramref name="fromActionIdx"/>.
/// No before/after step events fire (the step was already in flight when the
/// suspend happened). Mirrors RunAsync's action-loop semantics.
/// </summary>
public partial class @this
{
    public async Task<data.@this> Resume(actor.context.@this context, int fromActionIdx)
    {
        data.@this result = context.Ok();
        if (fromActionIdx < 0 || fromActionIdx >= Code.Count) return result;

        // The step runs again — its frame is the step in play for its remaining actions
        global::app.call.@this frame;
        try { frame = context.call.Push(this); }
        catch (global::app.error.CallStackOverflowException ex)
        {
            return context.Error(context.call.Overflow(ex, Goal, this));
        }
        await using var _frame = frame;

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
