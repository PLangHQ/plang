using app.error;
using app.variable;
using Action = app.goal.step.action.@this;
using Call = app.callstack.call.@this;
using ExampleSpec = app.type.spec.Example;
using ActionSpec = app.type.spec.Action;

namespace app.module.action.error;

/// <summary>
/// Modifier: wraps an action with error matching, retry, and an on-error action chain.
/// On success, passes through untouched. On failure, applies filters (StatusCode, Key,
/// Message); if matched, either ignores, retries, or runs Actions — ordered by
/// Order (RetryFirst default, GoalFirst runs Actions before retry).
/// </summary>
[Action("handle", Cacheable = false)]
[Modifier(Order = 3)]
public partial class Handle : IContext, IModifier
{
    public static ExampleSpec[] ExamplesForLlm() => new[]
    {
        // Numeric error codes go to StatusCode (an int), regardless of whether
        // the source uses "on error 404" or "on error key 404" — `404` is
        // always a status code, not a string identifier.
        new ExampleSpec(
            "read %path%, on error 404, write out \"missing\", read fallback.txt, write to %content%",
            new[]
            {
                new ActionSpec("file", "read", new() { ["Path"] = "%path%" },
                    Modifiers: new[]
                    {
                        new ActionSpec("error", "handle", new()
                        {
                            ["StatusCode"] = 404,
                            ["Actions"] = new[]
                            {
                                new ActionSpec("output",   "write", new() { ["Data"] = "missing" }),
                                new ActionSpec("file",     "read",  new() { ["Path"] = "fallback.txt" }),
                                new ActionSpec("variable", "set",   new() { ["Name"]  = "%content%",
                                                                             ["Value"] = "%!data%" }),
                            }
                        })
                    }),
            }),
        // Named error keys (non-numeric identifiers) go to Key.
        new ExampleSpec(
            "save %doc%, on error key Conflict, write out \"already exists\"",
            new[]
            {
                new ActionSpec("file", "write", new() { ["Path"] = "%doc%" },
                    Modifiers: new[]
                    {
                        new ActionSpec("error", "handle", new()
                        {
                            ["Key"] = "Conflict",
                            ["Actions"] = new[]
                            {
                                new ActionSpec("output", "write", new() { ["Data"] = "already exists" }),
                            }
                        })
                    }),
            }),
    };

    public partial global::app.data.@this<global::app.type.item.number.@this>? StatusCode { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Key { get; init; }
    public partial global::app.data.@this<global::app.type.item.text.@this>? Message { get; init; }
    /// <summary>
    /// Action chain to run when the error matches. Preferred over Goal — lets a
    /// developer express "on error, log + fall back + notify" inline without
    /// wrapping it in a goal. Actions execute in order; %!data% flows between
    /// them just like the main step chain.
    /// </summary>
    public partial global::app.data.@this<global::app.type.item.list.@this<global::app.goal.step.action.@this>>? Actions { get; init; }
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
            if (!MatchesError(result.Error)) return result;

            // Failing Call comes from the error's CallFrames snapshot — App.Run pushed and
            // popped the action's Call inside next(), so we can't read it from stack.Current
            // anymore. CallFrames[0] is the failing Call itself (post-Push snapshot).
            var erroredCall = result.Error is global::app.error.Error errWithFrames
                && errWithFrames.CallFrames.Count > 0
                ? errWithFrames.CallFrames[0]
                : null;

            var order = (Order == null ? null : await Order.Value()) ?? ErrorOrder.RetryFirst;
            // The recovery chain is a plang list<action> — RunRecovery opens each row through its
            // own action door (row.Value<action>()), so params survive; no CLR peel that drops them.
            var actions = Actions == null ? null : await Actions.Value() as global::app.type.item.list.@this;
            bool hasRecovery = actions != null && actions.Count > 0;

            if (order == ErrorOrder.GoalFirst)
            {
                if (hasRecovery)
                {
                    var recoveryResult = await RunRecoveryWithErrorScope(actions!, context, result.Error!);
                    if (recoveryResult.Success)
                    {
                        if (erroredCall != null) erroredCall.Handled = true;
                        return recoveryResult;
                    }
                    result.Error!.ErrorChain.Add(recoveryResult.Error!);
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
                    var recoveryResult = await RunRecoveryWithErrorScope(actions!, context, result.Error!);
                    if (recoveryResult.Success)
                    {
                        if (erroredCall != null) erroredCall.Handled = true;
                        return recoveryResult;
                    }
                    result.Error!.ErrorChain.Add(recoveryResult.Error!);
                }
            }

            // IgnoreError is the final fallback — after retry and recovery are exhausted
            if (await IgnoreError.ToBooleanAsync()) return context.Ok();

            return result;
        };
    }

    /// <summary>
    /// Runs recovery with <c>%!error%</c> populated to the caught error for the
    /// duration of the recovery chain, restoring the previous value via AsyncLocal LIFO
    /// scope on dispose.
    /// </summary>
    private static async Task<global::app.data.@this> RunRecoveryWithErrorScope(
        global::app.type.item.list.@this actions,
        actor.context.@this context,
        app.error.IError caughtError)
    {
        using (context.App.Error.Push(caughtError, context))
        {
            return await RunRecovery(actions, context);
        }
    }

    /// <summary>
    /// Runs the on-error recovery action chain.
    /// </summary>
    private static async Task<global::app.data.@this> RunRecovery(
        global::app.type.item.list.@this actions,
        actor.context.@this context)
    {
        // Nested actions live as parameter values with no Step reference of their own.
        // Stamp the enclosing step so navigation — goal.call → GetGoalAsync → sibling
        // sub-goals — works the same as for actions placed directly in a step.
        var enclosingStep = context.Step;
        global::app.data.@this last = context.Ok();
        foreach (var row in actions.Items)
        {
            // Each row opens through its OWN action door — a raw pr row becomes a real action
            // (params intact), not a CLR-peeled POCO.
            var action = await row.Value<Action>();
            if (action == null) continue;
            if (action.Step == null && enclosingStep != null)
                action.Step = enclosingStep;
            last = await action.Run(context);
            if (!last.Success) return last;
        }
        return last;
    }

    /// <summary>
    /// Matches the error against StatusCode / Key / Message filters.
    /// No filters = match all errors. Each supplied filter must match.
    /// </summary>
    private bool MatchesError(IError? error)
    {
        // MatchesError is a sync predicate — read the materialised backing, not the async door.
        // An UNSET filter resolves to the typed null citizen, never C# null — and its
        // ToString() renders "null", so presence MUST be tested via IsNull, never
        // string.IsNullOrEmpty(ToString()) (which would read an unset filter as the
        // literal "null" and spuriously activate it).
        var sc = StatusCode?.Peek();
        var key = Key?.Peek();
        var msg = Message?.Peek();
        bool hasKey = key is { IsNull: false };
        bool hasMsg = msg is { IsNull: false };

        if (sc is not global::app.type.item.number.@this && !hasKey && !hasMsg) return true;
        if (error == null) return false;

        // The matcher's int boundary is IError.StatusCode — the number lowers
        // itself there (Peek: sync predicate, value already in memory).
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
