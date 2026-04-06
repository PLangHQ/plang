using App;
using App.Errors;
using App.Goals.Goal.Steps.Step;
using App.Variables;

namespace App.modules.error;

/// <summary>
/// Single owner of all error handling: filtering, ignoreError, retry (with delay),
/// error goal, order (GoalFirst/RetryFirst), and propagation.
/// Reads the user step's OnError from Variables.
/// </summary>
[Action("check", Cacheable = false)]
public partial class Check : IContext, IAction
{
    public partial Data.@this Data { get; init; }
    public partial Step? Step { get; init; }

    public async Task<Data.@this> Run()
    {
        if (this.Data == null || this.Data.Success) return this.Data ?? App.Data.@this.Ok();

        var onError = Step?.OnError;

        // No error handler — propagate
        if (onError == null)
        {
            Data.Handled = false;
            return this.Data;
        }

        // Filter — does this error match the handler's criteria?
        if (!ErrorMatches(Data.Error, onError))
        {
            Data.Handled = false;
            return this.Data;
        }

        // Ignore — swallow
        if (onError.IgnoreError)
            return App.Data.@this.Ok();

        var engine = Context.App!;
        var order = onError.Order ?? ErrorOrder.RetryFirst;

        if (order == ErrorOrder.GoalFirst)
        {
            // Goal first (e.g. fix preconditions), then retry
            await CallErrorGoal(engine, Step!, onError);

            var retryResult = await Retry(engine, Step!);
            if (retryResult != null) return retryResult;

            // Goal ran but no retry or retry failed — goal alone counts as handled
            if (onError.Goal != null) return App.Data.@this.Ok();
        }
        else
        {
            // Retry first (default), then error goal as fallback
            var retryResult = await Retry(engine, Step!);
            if (retryResult != null) return retryResult;

            if (onError.Goal != null)
            {
                await CallErrorGoal(engine, Step!, onError);
                return App.Data.@this.Ok();
            }
        }

        // Nothing handled it — propagate
        Data.Handled = false;
        return this.Data;
    }

    /// <summary>
    /// Retry the step up to RetryCount times, with delay spread over RetryOverMs.
    /// Returns App.Data.@this.Ok() on success, null if retries exhausted or not configured.
    /// </summary>
    private async Task<Data.@this?> Retry(App.@this engine, App.Goals.Goal.Steps.Step.@this Step)
    {
        var onError = Step.OnError!;
        if (onError.RetryCount == null || onError.RetryCount <= 0) return null;

        var delayMs = onError.RetryOverMs != null && onError.RetryCount > 0
            ? onError.RetryOverMs.Value / onError.RetryCount.Value
            : 0;

        for (int attempt = 0; attempt < onError.RetryCount; attempt++)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs);

            // Re-execute the user step's actions on the user actor's context
            var userContext = engine.User.Context;
            App.Data.@this result = App.Data.@this.Ok();
            foreach (var action in Step.Actions)
            {
                result = await engine.Run(action, userContext);
                if (!result.Success) break;
            }

            if (result.Success) return result;
        }

        return null;
    }

    /// <summary>
    /// Call the error goal if configured. Stamps Action for sub-goal navigation.
    /// </summary>
    private async Task CallErrorGoal(App.@this engine,
        App.Goals.Goal.Steps.Step.@this Step, ErrorHandler onError)
    {
        if (onError.Goal == null) return;

        // Set error on context so %!error% resolves during the error goal
        var previousError = Context.CurrentError;
        Context.CurrentError = Data.Error;

        // Also set on callstack frame if available (for future %!error.Previous% support)
        var frame = Context.CallStack?.Current;
        if (frame != null) frame.Error = Data.Error;

        try
        {
            // Stamp Action so GoalCall can navigate to sub-goals
            onError.Goal.Action ??= Step.Actions.FirstOrDefault();
            await engine.RunGoalAsync(onError.Goal, Context);
        }
        finally
        {
            Context.CurrentError = previousError;
            if (frame != null) frame.Error = previousError;
        }
    }

    /// <summary>
    /// Check if the error matches the handler's filter criteria (message, statusCode, key).
    /// All specified filters must match. No filters = matches everything.
    /// </summary>
    private static bool ErrorMatches(IError? error, ErrorHandler onError)
    {
        if (error == null) return true;

        if (onError.Message != null &&
            !error.Message.Contains(onError.Message, StringComparison.OrdinalIgnoreCase))
            return false;

        if (onError.StatusCode != null && error.StatusCode != onError.StatusCode)
            return false;

        if (onError.Key != null &&
            !string.Equals(error.Key, onError.Key, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
