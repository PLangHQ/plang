using App.Errors;
using App.Variables;
using static App.Catalog.ExampleHelpers;
using ActionEntity = App.Goals.Goal.Steps.Step.Actions.Action.@this;
using Call = App.CallStack.Call.@this;

namespace App.modules.error;

/// <summary>
/// Modifier: wraps an action with error matching, retry, and an on-error action chain.
/// On success, passes through untouched. On failure, applies filters (StatusCode, Key,
/// Message); if matched, either ignores, retries, or runs Actions — ordered by
/// Order (RetryFirst default, GoalFirst runs Actions before retry).
/// </summary>
[ModuleDescription("Error handling: throw errors from a step, or wrap the preceding action with retry/handle-actions/ignore semantics")]
[System.ComponentModel.Description("Intercept errors from the preceding action; optionally retry, run a recovery action chain, or suppress the error")]
[Action("handle", Cacheable = false)]
[Modifier(Order = 3)]
public partial class Handle : IContext, IModifier
{
    public static App.Catalog.ExampleSpec[] ExamplesForLlm() => new[]
    {
        // Numeric error codes go to StatusCode (an int), regardless of whether
        // the source uses "on error 404" or "on error key 404" — `404` is
        // always a status code, not a string identifier.
        Example(
            "read %path%, on error 404, write out \"missing\", read fallback.txt, write to %content%",
            Action("file.read", new() { ["Path"] = "%path%" },
                modifiers: new[]
                {
                    Action("error.handle", new()
                    {
                        ["StatusCode"] = 404,
                        ["Actions"] = new[]
                        {
                            Action("output.write", new() { ["Data"] = "missing" }),
                            Action("file.read",    new() { ["Path"] = "fallback.txt" }),
                            Action("variable.set", new() { ["Name"]  = "%content%",
                                                            ["Value"] = "%__data__%" }),
                        }
                    })
                })
        ),
        // Named error keys (non-numeric identifiers) go to Key.
        Example(
            "save %doc%, on error key Conflict, write out \"already exists\"",
            Action("file.write", new() { ["Path"] = "%doc%" },
                modifiers: new[]
                {
                    Action("error.handle", new()
                    {
                        ["Key"] = "Conflict",
                        ["Actions"] = new[]
                        {
                            Action("output.write", new() { ["Data"] = "already exists" }),
                        }
                    })
                })
        )
    };

    public partial global::App.Data.@this<int>? StatusCode { get; init; }
    public partial global::App.Data.@this<string>? Key { get; init; }
    public partial global::App.Data.@this<string>? Message { get; init; }
    /// <summary>
    /// Action chain to run when the error matches. Preferred over Goal — lets a
    /// developer express "on error, log + fall back + notify" inline without
    /// wrapping it in a goal. Actions execute in order; %__data__% flows between
    /// them just like the main step chain.
    /// </summary>
    public partial global::App.Data.@this<List<ActionEntity>>? Actions { get; init; }
    public partial global::App.Data.@this<int>? RetryCount { get; init; }
    public partial global::App.Data.@this<int>? RetryOverMs { get; init; }
    public partial global::App.Data.@this<ErrorOrder>? Order { get; init; }
    [Default(false)]
    public partial global::App.Data.@this<bool> IgnoreError { get; init; }

    public Task<global::App.Data.@this> Run() => Task.FromResult(global::App.Data.@this.Ok());

    public Func<Task<global::App.Data.@this>> Wrap(Func<Task<global::App.Data.@this>> next, Actor.Context.@this context)
    {
        return async () =>
        {
            var result = await next();
            if (result.Success) return result;
            if (!MatchesError(result.Error)) return result;

            // Failing Call comes from the error's CallFrames snapshot — App.Run pushed and
            // popped the action's Call inside next(), so we can't read it from stack.Current
            // anymore. CallFrames[0] is the failing Call itself (post-Push snapshot).
            var erroredCall = result.Error is global::App.Errors.Error errWithFrames
                && errWithFrames.CallFrames.Count > 0
                ? errWithFrames.CallFrames[0]
                : null;

            var order = Order?.Value ?? ErrorOrder.RetryFirst;
            var actions = Actions?.Value;
            bool hasRecovery = actions != null && actions.Count > 0;

            if (order == ErrorOrder.GoalFirst)
            {
                if (hasRecovery)
                {
                    var recoveryResult = await RunRecoveryWithErrorScope(actions!, context, result.Error!, erroredCall);
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
                    var recoveryResult = await RunRecoveryWithErrorScope(actions!, context, result.Error!, erroredCall);
                    if (recoveryResult.Success)
                    {
                        if (erroredCall != null) erroredCall.Handled = true;
                        return recoveryResult;
                    }
                    result.Error!.ErrorChain.Add(recoveryResult.Error!);
                }
            }

            // IgnoreError is the final fallback — after retry and recovery are exhausted
            if (IgnoreError.Value) return global::App.Data.@this.Ok();

            return result;
        };
    }

    /// <summary>
    /// Runs recovery with <c>%!error%</c> populated to the caught error for the
    /// duration of the recovery chain, restoring the previous value via AsyncLocal LIFO
    /// scope on dispose. Each recovery action's Call is dispatched with
    /// <paramref name="erroredCall"/> as Cause — so renderers see "this happened because
    /// of that errored sibling."
    /// </summary>
    private static async Task<global::App.Data.@this> RunRecoveryWithErrorScope(
        List<ActionEntity> actions,
        Actor.Context.@this context,
        App.Errors.IError caughtError,
        Call? erroredCall)
    {
        using (context.App.Errors.Push(caughtError))
        {
            return await RunRecovery(actions, context, erroredCall);
        }
    }

    /// <summary>
    /// Runs the on-error recovery action chain. Each action is dispatched through
    /// <c>App.Run</c> with <paramref name="cause"/> threaded through so the resulting Call
    /// has <c>Cause = erroredCall</c> in addition to its sync Caller (the goal-level Call).
    /// </summary>
    private static async Task<global::App.Data.@this> RunRecovery(
        List<ActionEntity> actions,
        Actor.Context.@this context,
        Call? cause)
    {
        // Nested actions live as parameter values with no Step reference of their own.
        // Stamp the enclosing step so navigation — goal.call → GetGoalAsync → sibling
        // sub-goals — works the same as for actions placed directly in a step.
        var enclosingStep = context.Step;
        global::App.Data.@this last = global::App.Data.@this.Ok();
        foreach (var action in actions)
        {
            if (action.Step == null && enclosingStep != null)
                action.Step = enclosingStep;
            last = await action.RunAsync(context, cause);
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
        if (StatusCode?.Value == null && Key?.Value == null && Message?.Value == null) return true;
        if (error == null) return false;

        if (StatusCode?.Value is int sc && error.StatusCode != sc) return false;
        if (!string.IsNullOrEmpty(Key?.Value)
            && !string.Equals(error.Key, Key.Value, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrEmpty(Message?.Value)
            && !error.Message.Contains(Message.Value!, StringComparison.OrdinalIgnoreCase)) return false;

        return true;
    }

    private async Task<global::App.Data.@this?> Retry(Func<Task<global::App.Data.@this>> next, Actor.Context.@this context)
    {
        var count = RetryCount?.Value;
        if (count == null || count <= 0) return null;

        var delayMs = RetryOverMs?.Value != null && count > 0
            ? RetryOverMs.Value / count.Value : 0;

        for (int attempt = 0; attempt < count; attempt++)
        {
            if (delayMs > 0) await Task.Delay(delayMs, context.CancellationToken);
            var result = await next();
            if (result.Success) return result;
        }
        return null;
    }

}
