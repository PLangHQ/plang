using App.Context;
using App.Errors;
using App.Variables;

namespace App.modules.app;

/// <summary>
/// Unified run action — runs a GoalCall, Step, or Action on an optional target actor.
/// Handles actor context switching and escalation checks.
/// </summary>
[Action("run")]
public partial class run : IContext
{
    public partial GoalCall? GoalName { get; init; }
    public partial Step? Step { get; init; }
    public partial Goals.Goal.Steps.Step.Actions.Action.@this? Action { get; init; }

    /// <summary>
    /// Target actor to execute as. Resolved from string ("user", "system") via Actor.Resolve.
    /// If null, executes on the current context.
    /// </summary>
    public partial Actor? Actor { get; init; }

    public async Task<Data.@this> Run()
    {
        var app = Context.App!;

        if (GoalName != null)
            return await RunGoal(app);

        // Actor context switching for Step/Action
        var (execContext, previousActor) = SwitchActor(app);
        if (execContext == null)
            return Error(new ActionError(
                $"Actor '{Context.Actor?.Name}' cannot escalate to '{Actor?.Name}'",
                "EscalationDenied", 403));

        try
        {
            if (Step != null)
                return await RunStep(app, execContext);

            if (Action != null)
                return await app.Run(Action, execContext);

            return Error(new ActionError(
                "run requires a GoalCall, Step, or Action", "MissingInput", 400));
        }
        finally
        {
            app.CurrentActor = previousActor;
        }
    }

    private async Task<Data.@this> RunGoal(App.@this app)
    {
        var goal = await GoalName!.GetGoalAsync(app, Context);
        if (goal == null)
            return Error(new ServiceError(
                $"Goal '{GoalName.Name ?? GoalName.PrPath}' not found", "NotFound", 404));

        return await app.RunGoalAsync(goal, Context, Context.CancellationToken);
    }

    private async Task<Data.@this> RunStep(App.@this app, Context.@this execContext)
    {
        if (Step!.Timeout is > 0)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(execContext.CancellationToken);
            timeoutCts.CancelAfter(Step.Timeout.Value);
            execContext.PushCancellation(timeoutCts);
            try
            {
                return await RunActions(app, execContext);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !execContext.CancellationToken.IsCancellationRequested)
            {
                return Error(new ServiceError(
                    $"Step timed out after {Step.Timeout}ms: {Step.Text}",
                    "Timeout", 408));
            }
            finally
            {
                execContext.PopCancellation();
            }
        }

        return await RunActions(app, execContext);
    }

    private async Task<Data.@this> RunActions(App.@this app, Context.@this execContext)
    {
        App.Data.@this result = App.Data.@this.Ok();
        foreach (var action in Step!.Actions)
        {
            execContext.CancellationToken.ThrowIfCancellationRequested();
            result = await app.Run(action, execContext);
            if (!result.Success) break;
        }
        return result;
    }

    /// <summary>
    /// Returns (execContext, previousActor) or (null, _) if escalation denied.
    /// </summary>
    private (Context.@this?, Actor?) SwitchActor(App.@this app)
    {
        var callingActor = Context.Actor;
        var targetActor = Actor;
        var previousActor = app.CurrentActor;

        if (targetActor != null && targetActor != callingActor)
        {
            if (callingActor != null && callingActor.EscalationLevel < targetActor.EscalationLevel)
                return (null, previousActor);

            app.CurrentActor = targetActor;
            return (targetActor.Context, previousActor);
        }

        return (Context, previousActor);
    }
}
