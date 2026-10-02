using app.error;

namespace app.module.on;

/// <summary>
/// <c>on.timeout</c> — a clause of the action before it: each attempt has a deadline, <see cref="After"/> from
/// its start. Past it the attempt is cancelled and fails with Timeout (408) — an error the action's
/// <c>on.error</c> clauses catch like any other. A retry is a fresh attempt with a fresh deadline.
/// </summary>
[Action("timeout", Cacheable = false)]
public partial class OnTimeout : IContext, IClause
{
    /// <summary>How long an attempt may run.</summary>
    [IsNotNull]
    public partial global::app.data.@this<global::app.type.item.duration.@this> After { get; init; }

    public Task<global::app.data.@this> Start() => Task.FromResult(Context.Ok());

    public void Bind(global::app.goal.step.action.@this clause, global::app.goal.step.action.@this action)
    {
        var on = action.Own();
        on.Bind("start", global::app.@event.When.before, side => new global::app.@event.binding.clause.@this(
            side, clause, action, (handler, _, result, context) => ((OnTimeout)handler).Begin(result, context)));
        on.Bind("start", global::app.@event.When.after, side => new global::app.@event.binding.clause.@this(
            side, clause, action, (handler, _, result, context) => ((OnTimeout)handler).End(result, context)));
    }

    // This attempt's deadline, held on the action's frame from before its start to after it; its after takes it
    // (Spent) so an attempt whose before never ran pops nothing.
    private sealed record Deadline(CancellationTokenSource Cts, CancellationToken Parent, System.TimeSpan After, bool Spent);

    /// <summary>Before an attempt starts: its deadline is pushed where cancellation lives — the context's — so
    /// everything the attempt runs reads it.</summary>
    public async Task<global::app.data.@this> Begin(global::app.data.@this result, actor.context.@this context)
    {
        var after = (System.TimeSpan)(await After.Value())!;
        // The parent token is read BEFORE the push — after it, context.CancellationToken is our own.
        var parent = context.CancellationToken;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(parent);
        cts.CancelAfter(after);
        context.PushCancellation(cts);
        context.call.Current?.SetItem(new Deadline(cts, parent, after, Spent: false));
        return result;
    }

    /// <summary>After an attempt: its deadline is popped; past it, the verdict is the deadline's — a result that
    /// arrives late is late, whether or not the work also failed.</summary>
    public Task<global::app.data.@this> End(global::app.data.@this result, actor.context.@this context)
    {
        var frame = context.call.Current;
        if (frame?.GetItem<Deadline>() is not { Spent: false } deadline) return Task.FromResult(result);
        frame.SetItem(deadline with { Spent = true });
        context.PopCancellation();
        using var cts = deadline.Cts;
        // Parent cancelled — the cancellation propagates up (not our timeout).
        if (deadline.Parent.IsCancellationRequested) throw new OperationCanceledException(deadline.Parent);
        if (!cts.IsCancellationRequested) return Task.FromResult(result);
        var timedOut = new ServiceError($"Timed out after {deadline.After}", "Timeout", 408);
        frame.Record(timedOut, context);
        return Task.FromResult(context.Error(timedOut));
    }
}
