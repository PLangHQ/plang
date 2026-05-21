using App.Data;

namespace App.Goals.Goal.Steps.Step;

/// <summary>
/// Continuation entry for resume — runs actions starting at <paramref name="fromActionIdx"/>.
/// No before/after step events fire (the step was already in flight when the
/// suspend happened). Mirrors RunAsync's action-loop semantics.
/// </summary>
public partial class @this
{
    public async Task<Data.@this> RunFrom(Actor.Context.@this context, int fromActionIdx)
    {
        context.Step = this;

        Data.@this result = Data.@this.Ok();
        if (fromActionIdx < 0 || fromActionIdx >= Actions.Count) return result;

        try
        {
            for (int i = fromActionIdx; i < Actions.Count; i++)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                result = await Actions[i].RunAsync(context);
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
            result = Data.@this.FromError(new Errors.ServiceError(
                ex.Message, key, 400) { Exception = ex });
        }

        return result;
    }
}
