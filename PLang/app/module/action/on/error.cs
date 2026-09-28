using Action = app.goal.step.action.@this;

namespace app.module.action.on;

/// <summary>
/// <c>on.error</c> — a clause of the action before it: what answers when that action fails. Bound on its error
/// outcome, which fires after the attempt; several clauses answer in the order written, and the first whose
/// filters (StatusCode, Key, Message) match takes the failure — the others never see it. It retries (each retry a
/// fresh attempt), runs its <see cref="Recovery"/>, or ignores — ordered by Order (RetryFirst default, GoalFirst
/// runs the recovery before retrying). The class is <c>OnError</c>, not <c>Error</c>: every action carries a
/// generated <c>Error</c> member, and a member can't share its class's name.
/// </summary>
[Action("error", Cacheable = false)]
public partial class OnError : IContext, IClause, IAction
{
    public partial global::app.data.@this<global::app.type.item.number.@this>? StatusCode { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Key { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Message { get; init; }
    public partial global::app.data.@this<global::app.type.item.number.@this>? RetryCount { get; init; }
    /// <summary>How long the retries take in all — spread evenly between them.</summary>
    public partial global::app.data.@this<global::app.type.item.duration.@this>? RetryOver { get; init; }
    public partial global::app.data.@this<global::app.type.item.choice.@this<ErrorOrder>>? Order { get; init; }
    [Default(false)]
    public partial global::app.data.@this<global::app.type.item.@bool.@this> IgnoreError { get; init; }
    /// <summary>The actions that run to recover — the body of the clause (a goal call, a set).</summary>
    public partial global::app.data.@this<global::app.goal.step.action.list.@this>? Recovery { get; init; }

    public Task<global::app.data.@this> Start() => Task.FromResult(Context.Ok());

    public void Bind(Action clause, Action action)
        => action.Own().Bind("error", global::app.@event.When.before, side => new global::app.@event.binding.clause.@this(
            side, clause, action, (handler, a, failed, context) => ((OnError)handler).Catch(a, failed, context)));

    /// <summary>This clause's answer to <paramref name="action"/>'s failure: the failure itself, handed back, when
    /// its filters don't match (the next clause gets its turn); otherwise its own answer — ordered by Order — of
    /// its retry, its recovery, its ignore, or the error as it stands.</summary>
    public async Task<global::app.data.@this> Catch(Action action, global::app.data.@this failed, actor.context.@this context)
    {
        if (!await MatchesError(failed.Error)) return failed;

        // The failing frame is LIVE and it is the current one: the error outcome fires inside the frame the
        // action pushed for its whole run, so the frame that recorded the error is the one we stand in. Marking
        // it Handled is what takes the error out of play for %!error%.
        var erroredCall = context.CallStack.Current;

        var order = (Order == null ? null : await Order.Value()) ?? ErrorOrder.RetryFirst;
        var recovery = Recovery == null ? null : await Recovery.Value();
        bool hasRecovery = recovery != null && recovery.CountRaw > 0;

        if (order == ErrorOrder.GoalFirst)
        {
            // Fix, then retry: the handler goal runs first, then the step retries (RetryCount
            // times) and gets the retry's result. With no retry configured the handler's result
            // stands. A handler that fails still lets the retry run — the failure may not have
            // needed its fix (a transient one) — and its error joins the list.
            global::app.data.@this? recoveryResult = null;
            if (hasRecovery)
            {
                recoveryResult = await Recover(recovery!, context);
                if (!recoveryResult.Success) Chain(failed.Error!, recoveryResult.Error!);
            }
            var retryResult = await Retry(action, context);
            if (retryResult != null)
            {
                if (retryResult.Success && erroredCall != null) erroredCall.Handled = true;
                return retryResult;
            }
            if (recoveryResult is { Success: true })
            {
                if (erroredCall != null) erroredCall.Handled = true;
                return recoveryResult;
            }
        }
        else
        {
            var retryResult = await Retry(action, context);
            if (retryResult?.Success == true) return retryResult;
            if (hasRecovery)
            {
                var recoveryResult = await Recover(recovery!, context);
                if (recoveryResult.Success)
                {
                    if (erroredCall != null) erroredCall.Handled = true;
                    return recoveryResult;
                }
                Chain(failed.Error!, recoveryResult.Error!);
            }
        }

        // IgnoreError is the final fallback — after retry and recovery are exhausted. The error
        // stays in the audit; the frame is marked handled, and the ignore is visible under --debug.
        if (await IgnoreError.ToBooleanAsync())
        {
            if (erroredCall != null) erroredCall.Handled = true;
            await (context.App.Debug?.Write(
                $"on.error: ignored error {failed.Error?.Key}: {failed.Error?.Message}") ?? Task.CompletedTask);
            return context.Ok();
        }

        // Taken but not recovered: the error stands, answered by THIS clause — a Data of its own, so the outcome
        // knows it was taken and no later clause sees it.
        return context.Error(failed.Error!);
    }

    /// <summary>The recovery's failure joins the original error's list — unless the recovery re-raised
    /// that same error (<c>throw %!error%</c>), which is the error itself, not a cause of it.</summary>
    private void Chain(global::app.error.Error original, global::app.error.Error recovery)
    {
        if (!ReferenceEquals(original, recovery)) original.list.Add(recovery);
    }

    /// <summary>
    /// Runs this clause's recovery under a diff-capture scope, so handler-time mutations land on the
    /// CallStack's diff stream and <c>Variables.SnapshotAt</c> can project back to throw-time state.
    /// <c>%!error%</c> needs nothing here — the error is on the frame we are standing in, and
    /// <c>CallStack.Error</c> reads it there.
    /// </summary>
    private async Task<global::app.data.@this> Recover(global::app.goal.step.action.list.@this recovery, actor.context.@this context)
    {
        using (context.CallStack.DiffScope(context.Variable))
        {
            return await recovery.Start(context);
        }
    }

    /// <summary>
    /// Matches the error against StatusCode / Key / Message filters.
    /// No filters = match all errors. Each supplied filter must match.
    /// </summary>
    private async Task<bool> MatchesError(global::app.error.Error? error)
    {
        // The filters are read through the typed ask, not a sync Peek. A .pr-loaded parameter is
        // lazy — it lifts on the ask — so a Peek here sees the wire form rather than the value.
        // An UNSET filter resolves to the typed null citizen, never C# null — and its ToString()
        // renders "null", so presence MUST be tested via IsNull.
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

    /// <summary>Retries the action RetryCount times — each a fresh attempt — spread over RetryOver. Answers the
    /// first success, or the last attempt's failure; null when no retry is configured.</summary>
    private async Task<global::app.data.@this?> Retry(Action action, actor.context.@this context)
    {
        var retries = RetryCount == null ? null : await RetryCount.Value();
        if (retries == null) return null;
        int count = retries.ToInt32();
        if (count <= 0) return null;

        var over = RetryOver == null ? null : await RetryOver.Value();
        System.TimeSpan pause = over is { IsNull: false } ? (System.TimeSpan)over / count : System.TimeSpan.Zero;

        global::app.data.@this? last = null;
        for (int attempt = 0; attempt < count; attempt++)
        {
            if (pause > System.TimeSpan.Zero) await Task.Delay(pause, context.CancellationToken);
            last = await action.Attempt(context);
            if (last.Success) return last;
        }
        return last;
    }
}
