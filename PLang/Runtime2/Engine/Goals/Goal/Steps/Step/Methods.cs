using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Events;

namespace PLang.Runtime2.Engine.Goals.Steps;

public sealed partial class Step
{
    public async Task<Data> Load(PLangContext context)
    {
        var lifecycle = context.LifecycleFor(this);
        var before = await lifecycle.Before.Run(context, EventType.OnBeforeStepLoad);
        if (!before.Success) return before;

        var actionsResult = await Actions.Load(context);
        if (!actionsResult.Success) return actionsResult;

        var after = await lifecycle.After.Run(context, EventType.OnAfterStepLoad);
        return after;
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken cancellationToken = default)
    {
        context.Step = this;
        context.CallStack?.RecordStep(this);

        var lifecycle = context.LifecycleFor(this);

        Data beforeResult;
        try
        {
            beforeResult = await lifecycle.Before.Run(context, EventType.BeforeStep);
        }
        catch (Exception ex)
        {
            var eventError = StepError.FromException(ex, context);
            context.CallStack?.AddError(eventError);
            return Data.FromError(eventError);
        }
        if (!beforeResult) return beforeResult;
        if (beforeResult.Handled) return beforeResult;

        Data result;
        try
        {
            result = await ExecuteActionsAsync(engine, context, cancellationToken);
        }
        catch (Exception ex)
        {
            var error = StepError.FromException(ex, context);
            context.CallStack?.AddError(error);
            result = Data.FromError(error);
        }

        // Handle errors via OnError configuration
        if (!result.Success && OnError != null)
        {
            try
            {
                result = await HandleErrorAsync(result, engine, context, cancellationToken);
            }
            catch (Exception ex)
            {
                var handlerError = StepError.FromException(ex, context);
                context.CallStack?.AddError(handlerError);
                // Chain: original error + error-handling failure
                if (result.Error is Error originalError)
                    originalError.ErrorChain.Add(handlerError);
                result = Data.FromError(handlerError);
            }
        }

        if (!result.Success) return result;

        try
        {
            var afterResult = await lifecycle.After.Run(context, EventType.AfterStep);
            if (!afterResult) return afterResult;
        }
        catch (Exception ex)
        {
            var eventError = StepError.FromException(ex, context);
            context.CallStack?.AddError(eventError);
            return Data.FromError(eventError);
        }

        return result;
    }

    private async Task<Data> ExecuteActionsAsync(Engine engine, PLangContext context, CancellationToken cancellationToken)
    {
        return StepCache != null
            ? await StepCache.RunAsync(engine, context, cancellationToken)
            : await Actions.RunAsync(engine, context, cancellationToken);
    }

    private async Task<Data> HandleErrorAsync(Data failedResult, Engine engine, PLangContext context, CancellationToken cancellationToken)
    {
        var handler = OnError;
        if (handler == null) return failedResult;

        // Determine order of operations
        var order = handler.Order ?? ErrorOrder.RetryFirst;

        if (order == ErrorOrder.RetryFirst)
        {
            // Try retries first, then call error goal if still failing
            var retryResult = await RetryAsync(handler, engine, context, cancellationToken);
            if (retryResult.Success) return retryResult;

            var goalResult = await CallErrorGoalAsync(handler, failedResult, engine, context, cancellationToken);
            if (goalResult != null) return goalResult;
        }
        else
        {
            // Call error goal first, then retry
            var goalResult = await CallErrorGoalAsync(handler, failedResult, engine, context, cancellationToken);
            if (goalResult != null && goalResult.Success) return goalResult;

            var retryResult = await RetryAsync(handler, engine, context, cancellationToken);
            if (retryResult.Success) return retryResult;
        }

        // If IgnoreError, return success
        if (handler.IgnoreError)
            return Data.Ok();

        return failedResult;
    }

    private async Task<Data> RetryAsync(ErrorHandler handler, Engine engine, PLangContext context, CancellationToken cancellationToken)
    {
        var retryCount = handler.RetryCount ?? 0;
        if (retryCount <= 0) return Data.FromError(new Error("No retries configured"));

        var totalSeconds = handler.RetryOverSeconds ?? 0;
        var delayMs = retryCount > 1 && totalSeconds > 0
            ? (int)(totalSeconds * 1000.0 / (retryCount - 1))
            : 0;

        for (int attempt = 0; attempt < retryCount; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Data.FromError(GoalError.Cancelled(context));

            if (attempt > 0 && delayMs > 0)
            {
                try { await Task.Delay(delayMs, cancellationToken); }
                catch (OperationCanceledException)
                {
                    return Data.FromError(GoalError.Cancelled(context));
                }
            }

            try
            {
                var result = await ExecuteActionsAsync(engine, context, cancellationToken);
                if (result.Success) return result;
            }
            catch
            {
                // Retry on exception
            }
        }

        return Data.FromError(new Error($"Step failed after {retryCount} retries", "RetryExhausted", 500));
    }

    private async Task<Data?> CallErrorGoalAsync(ErrorHandler handler, Data failedResult, Engine engine, PLangContext context, CancellationToken cancellationToken)
    {
        if (handler.Goal == null) return null;

        // Set error info in memory so the error goal can access it
        context.MemoryStack.Set("__error__", failedResult.Error?.Message);
        context.MemoryStack.Set("__errorKey__", failedResult.Error?.Key);
        context.MemoryStack.Set("__errorStatusCode__", failedResult.Error?.StatusCode);

        try
        {
            var goalResult = await engine.RunGoalAsync(handler.Goal, context, cancellationToken);
            return goalResult;
        }
        catch (Exception ex)
        {
            // Error during error handling — chain it
            if (failedResult.Error is Error originalError)
            {
                originalError.ErrorChain.Add(Error.FromException(ex));
            }
            return null;
        }
        finally
        {
            // Clean up error variables
            context.MemoryStack.Remove("__error__");
            context.MemoryStack.Remove("__errorKey__");
            context.MemoryStack.Remove("__errorStatusCode__");
        }
    }
}
