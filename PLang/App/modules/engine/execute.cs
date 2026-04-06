using App.Engine.Context;
using App.Engine.Errors;
using App.Engine.Variables;

namespace App.modules.engine;

/// <summary>
/// Kernel step dispatch — runs a step's actions on the target actor's context.
/// Handles context switching and escalation checks.
/// </summary>
[Action("execute")]
public partial class Execute : IContext
{
    [IsNotNull]
    public partial Step Step { get; init; }

    /// <summary>
    /// Target actor to execute as. Resolved from string ("user", "service", "system") via Actor.Resolve.
    /// If null, executes on the current context.
    /// </summary>
    public partial Actor? Actor { get; init; }

    public async Task<Data> Run()
    {
        var engine = Context.Engine!;
        var callingActor = Context.Actor;
        var targetActor = Actor;

        // Determine execution context
        PLangContext execContext;
        if (targetActor != null && targetActor != callingActor)
        {
            // Escalation check: caller must have >= level of target
            if (callingActor != null && callingActor.EscalationLevel < targetActor.EscalationLevel)
                return Data.FromError(new ActionError(
                    $"Actor '{callingActor.Name}' cannot escalate to '{targetActor.Name}'",
                    "EscalationDenied", 403));

            execContext = targetActor.Context;
        }
        else
        {
            execContext = Context;
        }

        // Switch engine's current actor for the duration of execution
        var previousActor = engine.CurrentActor;
        if (targetActor != null) engine.CurrentActor = targetActor;

        try
        {
            if (Step.Timeout is > 0)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(execContext.CancellationToken);
                timeoutCts.CancelAfter(Step.Timeout.Value);
                execContext.PushCancellation(timeoutCts);
                try
                {
                    return await ExecuteActions(engine, execContext);
                }
                catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !execContext.CancellationToken.IsCancellationRequested)
                {
                    var result = Data.FromError(new ServiceError(
                        $"Step timed out after {Step.Timeout}ms: {Step.Text}",
                        "Timeout", 408));
                    result.Handled = true;
                    return result;
                }
                finally
                {
                    execContext.PopCancellation();
                }
            }

            return await ExecuteActions(engine, execContext);
        }
        finally
        {
            engine.CurrentActor = previousActor;
        }
    }

    private async Task<Data> ExecuteActions(Engine.@this engine, PLangContext execContext)
    {
        Data result = Data.Ok();
        foreach (var action in Step.Actions)
        {
            execContext.CancellationToken.ThrowIfCancellationRequested();
            result = await engine.Run(action, execContext);
            if (!result.Success) break;
        }

        result.Handled = true;
        return result;
    }
}
