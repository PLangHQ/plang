using app.error;

namespace app.module.action.timeout;

/// <summary>
/// Modifier: wraps an action with a hard deadline. Cancels the action if it
/// exceeds Ms milliseconds and returns a 408 Timeout error.
/// </summary>
// timeout.after bounds one ATTEMPT, so it is innermost: each retry gets a fresh deadline, and an
// enclosing on.error catches a Timeout like any other error.
[Action("after", Cacheable = false)]
[Modifier(Order = 3)]
public partial class After : IContext, IModifier
{
    [IsNotNull]
    public partial global::app.data.@this<global::app.type.item.number.@this> Ms { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(Context.Ok());

    public Func<Task<global::app.data.@this>> Wrap(Func<Task<global::app.data.@this>> next, actor.context.@this context)
    {
        return async () =>
        {
            // The deadline is read through the typed door, INSIDE the async delegate: Wrap itself
            // is sync, and a param loaded from a .pr is lazy, so a sync Peek here sees an unresolved
            // value and has to invent a number. It used to invent 0 — "expire immediately" — which
            // is the most destructive reading available. The number lowers itself at CancelAfter,
            // the .NET edge that needs an int.
            int ms = (await Ms.Value()).ToInt32();

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

                // Our deadline fired, so the verdict is ours: a result arriving after the deadline
                // is late, and late is what a deadline forbids. It does not matter whether the
                // inner action also failed — reading its result to decide would let a success that
                // beat the cancellation home anyway overrule the deadline.
                if (cts.IsCancellationRequested)
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
