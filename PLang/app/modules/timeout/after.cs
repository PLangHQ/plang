using app.Errors;

namespace app.modules.timeout;

/// <summary>
/// Modifier: wraps an action with a hard deadline. Cancels the action if it
/// exceeds Ms milliseconds and returns a 408 Timeout error.
/// </summary>
[ModuleDescription("Modifier that wraps any action with a hard deadline, returning a 408 error on expiry")]
[System.ComponentModel.Description("Cancel the decorated action and return a timeout error if it exceeds Ms milliseconds")]
[Action("after", Cacheable = false)]
[Modifier(Order = 1)]
public partial class After : IContext, IModifier
{
    [IsNotNull]
    public partial global::app.Data.@this<int> Ms { get; init; }

    public Task<global::app.Data.@this> Run() => Task.FromResult(global::app.Data.@this.Ok());

    public Func<Task<global::app.Data.@this>> Wrap(Func<Task<global::app.Data.@this>> next, Actor.Context.@this context)
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
                    return global::app.Data.@this.FromError(new ServiceError(
                        $"Timed out after {ms}ms", "Timeout", 408));

                return result;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested
                && !parentToken.IsCancellationRequested)
            {
                // Fallback path: if an inner action re-throws OCE (some handlers don't wrap),
                // convert to Timeout error here.
                return global::app.Data.@this.FromError(new ServiceError(
                    $"Timed out after {ms}ms", "Timeout", 408));
            }
            finally
            {
                context.PopCancellation();
            }
        };
    }
}
