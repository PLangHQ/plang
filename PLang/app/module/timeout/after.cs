using app.error;

namespace app.module.timeout;

/// <summary>
/// Modifier: wraps an action with a hard deadline. Cancels the action if it
/// exceeds Ms milliseconds and returns a 408 Timeout error.
/// </summary>
[Action("after", Cacheable = false)]
[Modifier(Order = 1)]
public partial class After : IContext, IModifier
{
    [IsNotNull]
    public partial global::app.data.@this<global::app.type.item.number.@this> Ms { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(Context.Ok());

    public Func<Task<global::app.data.@this>> Wrap(Func<Task<global::app.data.@this>> next, actor.context.@this context)
    {
        // Sync wrap seam — Peek (the .pr literal is in memory); the number
        // lowers itself at the CancelAfter .NET edge.
        int ms = (Ms.Peek() as global::app.type.item.number.@this)?.ToInt32() ?? 0;
        return async () =>
        {
            // Capture parent token BEFORE pushing — after the push, context.CancellationToken
            // returns our own CTS, making the when-filter always false.
            var parentToken = context.CancellationToken;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            cts.CancelAfter(ms);
            context.PushCancellation(cts);
            try
            {
                var result = await next();

                // Parent cancelled — let the cancellation propagate up (not our timeout).
                if (parentToken.IsCancellationRequested)
                    throw new OperationCanceledException(parentToken);

                // Our timeout fired. Inner action's generated ExecuteAsync swallows OCE into a
                // ServiceError result, so we detect the timeout via CTS state + failed result.
                if (cts.IsCancellationRequested && !result.Success)
                    return context.Error(new ServiceError(
                        $"Timed out after {ms}ms", "Timeout", 408));

                return result;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested
                && !parentToken.IsCancellationRequested)
            {
                // Fallback path: if an inner action re-throws OCE (some handlers don't wrap),
                // convert to Timeout error here.
                return context.Error(new ServiceError(
                    $"Timed out after {ms}ms", "Timeout", 408));
            }
            finally
            {
                context.PopCancellation();
            }
        };
    }
}
