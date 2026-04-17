using App.Errors;

namespace App.modules.timeout;

/// <summary>
/// Modifier: wraps an action with a hard deadline. Cancels the action if it
/// exceeds Ms milliseconds and returns a 408 Timeout error.
/// </summary>
[Action("after", Cacheable = false)]
[Modifier(Order = 1)]
public partial class After : IContext, IModifier
{
    [IsNotNull]
    public partial global::App.Data.@this<int> Ms { get; init; }

    public Task<global::App.Data.@this> Run() => Task.FromResult(global::App.Data.@this.Ok());

    public Func<Task<global::App.Data.@this>> Wrap(Func<Task<global::App.Data.@this>> next, Actor.Context.@this context)
    {
        var ms = Ms.Value;
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
                    return global::App.Data.@this.FromError(new ServiceError(
                        $"Timed out after {ms}ms", "Timeout", 408));

                return result;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested
                && !parentToken.IsCancellationRequested)
            {
                // Fallback path: if an inner action re-throws OCE (some handlers don't wrap),
                // convert to Timeout error here.
                return global::App.Data.@this.FromError(new ServiceError(
                    $"Timed out after {ms}ms", "Timeout", 408));
            }
            finally
            {
                context.PopCancellation();
            }
        };
    }
}
