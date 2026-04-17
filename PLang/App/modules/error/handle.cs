using App.Errors;
using App.Variables;

namespace App.modules.error;

/// <summary>
/// Modifier: wraps an action with error matching, retry, and error goal handling.
/// On success, passes through untouched. On failure, applies filters (StatusCode, Key,
/// Message); if matched, either ignores, retries, or calls an error goal — ordered by
/// Order (RetryFirst default, GoalFirst calls the error goal before retry).
/// </summary>
[Action("handle", Cacheable = false)]
[Modifier(Order = 3)]
public partial class Handle : IContext, IModifier
{
    public partial global::App.Data.@this<int>? StatusCode { get; init; }
    public partial global::App.Data.@this<string>? Key { get; init; }
    public partial global::App.Data.@this<string>? Message { get; init; }
    public partial global::App.Data.@this<GoalCall>? Goal { get; init; }
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

            var order = Order?.Value ?? ErrorOrder.RetryFirst;
            var goal = Goal?.Value;

            if (order == ErrorOrder.GoalFirst)
            {
                if (goal != null)
                {
                    var goalResult = await CallErrorGoal(goal, result, context);
                    if (goalResult.Success) return goalResult;
                    // Goal itself failed — chain its error onto the original
                    result.Error!.ErrorChain.Add(goalResult.Error!);
                }
                var retryResult = await Retry(next, context);
                if (retryResult?.Success == true) return retryResult;
            }
            else
            {
                var retryResult = await Retry(next, context);
                if (retryResult?.Success == true) return retryResult;
                if (goal != null)
                {
                    var goalResult = await CallErrorGoal(goal, result, context);
                    if (goalResult.Success) return global::App.Data.@this.Ok();
                    // Goal itself failed — chain its error onto the original
                    result.Error!.ErrorChain.Add(goalResult.Error!);
                }
            }

            // IgnoreError is the final fallback — after retry and goal are exhausted
            if (IgnoreError.Value) return global::App.Data.@this.Ok();

            return result;
        };
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

    private async Task<global::App.Data.@this> CallErrorGoal(GoalCall goalCall, global::App.Data.@this failedResult,
        Actor.Context.@this context)
    {
        var parameters = (goalCall.Parameters ?? new())
            .Where(p => p.Name != "!error")
            .Append(new global::App.Data.@this("!error", failedResult.Error))
            .ToList();

        // Clone — never mutate the shared deserialized GoalCall singleton
        var call = new GoalCall
        {
            Name = goalCall.Name,
            Description = goalCall.Description,
            Parallel = goalCall.Parallel,
            Parameters = parameters,
            PrPath = goalCall.PrPath,
            Action = context.Step?.Actions.FirstOrDefault() ?? goalCall.Action
        };

        // Record error on the callstack for history
        var callStack = context.CallStack;
        if (callStack != null)
        {
            var action = context.Step?.Actions.FirstOrDefault();
            if (action != null && failedResult.Error != null)
                callStack.PushError(action, failedResult.Error, context.Variables);
        }

        return await context.App!.RunGoalAsync(call, context);
    }
}
