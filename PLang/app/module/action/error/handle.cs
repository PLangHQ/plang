using app.error;
using app.variable;
using Action = app.goal.step.action.@this;
using Call = app.callstack.call.@this;

namespace app.module.action.error;

/// <summary>
/// Modifier: wraps an action with error matching, retry, and an on-error action chain.
/// On success, passes through untouched. On failure, applies filters (StatusCode, Key,
/// Message); if matched, either ignores, retries, or runs its actions — ordered by
/// Order (RetryFirst default, GoalFirst runs the actions before retry).
/// </summary>
// A modifier's Order follows what it BOUNDS, lowest outermost. error.handle bounds the ATTEMPTS —
// every retry, and the recovery that follows them — so it is outermost and catches a deadline like
// any other error.
[Action("handle", Cacheable = false)]
[Modifier(Order = 1)]
public partial class Handle : IContext, IModifier, IAction
{
    public partial global::app.data.@this<global::app.type.item.number.@this>? StatusCode { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Key { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Message { get; init; }
    public partial global::app.data.@this<global::app.type.item.number.@this>? RetryCount { get; init; }
    public partial global::app.data.@this<global::app.type.item.number.@this>? RetryOverMs { get; init; }
    public partial global::app.data.@this<global::app.type.item.choice.@this<ErrorOrder>>? Order { get; init; }
    [Default(false)]
    public partial global::app.data.@this<global::app.type.item.@bool.@this> IgnoreError { get; init; }

    public Task<global::app.data.@this> Run() => Task.FromResult(Context.Ok());

    public Func<Task<global::app.data.@this>> Wrap(Func<Task<global::app.data.@this>> next, actor.context.@this context)
    {
        return async () =>
        {
            var result = await next();
            if (result.Success) return result;
            if (!await MatchesError(result.Error)) return result;

            // The failing Call is LIVE and it is the current one: an action owns one frame for
            // its whole run, and this modifier is wrapped inside that frame, so the frame that
            // recorded the error is still the frame we are standing in. Marking it Handled is
            // what takes the error out of play for %!error%.
            var erroredCall = context.CallStack.Current;

            var order = (Order == null ? null : await Order.Value()) ?? ErrorOrder.RetryFirst;
            bool hasRecovery = Action.Recovery.Count > 0;

            if (order == ErrorOrder.GoalFirst)
            {
                if (hasRecovery)
                {
                    var recoveryResult = await Recover(context);
                    if (recoveryResult.Success)
                    {
                        if (erroredCall != null) erroredCall.Handled = true;
                        return recoveryResult;
                    }
                    result.Error!.list.Add(recoveryResult.Error!);
                }
                var retryResult = await Retry(next, context);
                if (retryResult?.Success == true) return retryResult;
            }
            else
            {
                var retryResult = await Retry(next, context);
                if (retryResult?.Success == true) return retryResult;
                if (hasRecovery)
                {
                    var recoveryResult = await Recover(context);
                    if (recoveryResult.Success)
                    {
                        if (erroredCall != null) erroredCall.Handled = true;
                        return recoveryResult;
                    }
                    result.Error!.list.Add(recoveryResult.Error!);
                }
            }

            // IgnoreError is the final fallback — after retry and recovery are exhausted. The error
            // stays in the audit; the frame is marked handled, and the ignore is visible under --debug.
            if (await IgnoreError.ToBooleanAsync())
            {
                if (erroredCall != null) erroredCall.Handled = true;
                await (context.App.Debug?.Write(
                    $"error.handle: ignored error {result.Error?.Key}: {result.Error?.Message}") ?? Task.CompletedTask);
                return context.Ok();
            }

            return result;
        };
    }

    /// <summary>
    /// Runs this modifier's recovery chain under a diff-capture scope, so handler-time mutations
    /// land on the CallStack's diff stream and <c>Variables.SnapshotAt</c> can project back to
    /// throw-time state. <c>%!error%</c> needs nothing here — the error is on the frame we are
    /// standing in, and <c>CallStack.Error</c> reads it there.
    /// </summary>
    private async Task<global::app.data.@this> Recover(actor.context.@this context)
    {
        using (context.CallStack.DiffScope(context.Variable))
        {
            return await Action.Recovery.Run(context);
        }
    }

    /// <summary>
    /// Matches the error against StatusCode / Key / Message filters.
    /// No filters = match all errors. Each supplied filter must match.
    /// </summary>
    private async Task<bool> MatchesError(global::app.error.Error? error)
    {
        // The filters are read through the typed ask, not a sync Peek. A .pr-loaded parameter is
        // lazy — it lifts on the ask — so a Peek here sees the wire form rather than the value, and
        // the comparison below would silently match the wrong errors. This runs inside the delegate,
        // where awaiting is free.
        // An UNSET filter resolves to the typed null citizen, never C# null — and its ToString()
        // renders "null", so presence MUST be tested via IsNull, never
        // string.IsNullOrEmpty(ToString()) (which would read an unset filter as the literal "null"
        // and spuriously activate it).
        var sc = StatusCode == null ? null : await StatusCode.Value();
        var key = Key == null ? null : await Key.Value();
        var msg = Message == null ? null : await Message.Value();
        bool hasKey = key is { IsNull: false };
        bool hasMsg = msg is { IsNull: false };

        if (sc is not global::app.type.item.number.@this && !hasKey && !hasMsg) return true;
        if (error == null) return false;

        // The matcher's int boundary is Error.StatusCode — the number lowers itself there.
        if (sc is global::app.type.item.number.@this scNum && error.StatusCode != scNum.ToInt32()) return false;
        if (hasKey && !string.Equals(error.Key, key!.ToString(), StringComparison.OrdinalIgnoreCase)) return false;
        if (hasMsg && !error.Message.Contains(msg!.ToString()!, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private async Task<global::app.data.@this?> Retry(Func<Task<global::app.data.@this>> next, actor.context.@this context)
    {
        // Typed reads; the numbers lower at Task.Delay / the loop bound — the
        // handler's own .NET edges.
        var retries = RetryCount == null ? null : await RetryCount.Value();
        if (retries == null) return null;
        int count = retries.ToInt32();
        if (count <= 0) return null;

        var over = RetryOverMs == null ? null : await RetryOverMs.Value();
        int delayMs = over != null ? over.ToInt32() / count : 0;

        for (int attempt = 0; attempt < count; attempt++)
        {
            if (delayMs > 0) await Task.Delay(delayMs, context.CancellationToken);
            var result = await next();
            if (result.Success) return result;
        }
        return null;
    }

}
