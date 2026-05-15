using app;
using app.variables;
using app.events;
using EventBinding = app.events.lifecycle.bindings.binding.@this;

namespace app.modules.@event;

/// <summary>
/// Registers an event binding on the execution lifecycle.
/// Consolidates all event types (BeforeGoal, AfterGoal, BeforeStep, AfterStep, BeforeAction, AfterAction)
/// into a single action with a Type parameter. Returns the binding ID for later removal.
/// </summary>
[ModuleDescription("Lifecycle event hooks: register callbacks that run before or after goals, steps, or actions")]
[System.ComponentModel.Description("Register a goal callback to fire at a lifecycle event (BeforeGoal, AfterStep, BeforeAction, etc.)")]
[Example("before step, call LogStep, on goal pattern 'Api/*'",
    "event.on Type([eventtype] BeforeStep), GoalToCall([goal.call] LogStep), GoalPattern([string] Api/*)")]
[Action("on", Cacheable = false)]
public partial class On : IContext
{
    /// <summary>Event type: BeforeGoal, AfterGoal, BeforeStep, AfterStep, BeforeAction, AfterAction.</summary>
    [IsNotNull]
    public partial data.@this<EventType> Type { get; init; }
    /// <summary>Goal to execute when the event fires.</summary>
    public partial data.@this<GoalCall> GoalToCall { get; init; }
    /// <summary>Glob or regex pattern to match goal names. Null matches all goals.</summary>
    public partial data.@this<string>? GoalPattern { get; init; }
    /// <summary>Glob or regex pattern to match step text. Only for step-level events.</summary>
    public partial data.@this<string>? StepPattern { get; init; }
    /// <summary>Glob or regex pattern to match action names (e.g., "http.*"). Only for action-level events.</summary>
    public partial data.@this<string>? ActionPattern { get; init; }
    /// <summary>When true, patterns are treated as regular expressions instead of glob patterns.</summary>
    [Default(false)]
    public partial data.@this<bool> IsRegex { get; init; }
    /// <summary>Execution priority — higher values run first. Default is 0.</summary>
    [Default(0)]
    public partial data.@this<int> Priority { get; init; }

    /// <summary>Actor to bind the event to. If null, uses current actor.</summary>
    public partial data.@this<actor.@this>? Actor { get; init; }

    /// <summary>Channel-name filter for channel lifecycle events (BeforeWrite/AfterWrite/BeforeRead/AfterRead/OnAsk). Null = no filter.</summary>
    public partial data.@this<string>? ChannelName { get; init; }

    public Task<data.@this> Run()
    {
        // Resolve target actor — default to current context's actor
        var targetActor = Actor?.Value ?? Context.Actor ?? Context.App!.User;

        var goalToCall = GoalToCall.Value!;
        Func<actor.context.@this, goals.goal.steps.step.actions.action.@this?, data.@this?, Task<data.@this>> handler =
            async (ctx, _, _) => await ctx.App!.RunGoalAsync(goalToCall, targetActor.Context, ctx.CancellationToken);

        var binding = new EventBinding(
            Type.Value,
            handler,
            goalNamePattern: GoalPattern?.Value,
            stepPattern: StepPattern?.Value,
            actionPattern: ActionPattern?.Value,
            priority: Priority.Value,
            isRegex: IsRegex.Value,
            goalToCall: goalToCall,
            channelName: ChannelName?.Value);

        // Register on the target actor's event scope
        targetActor.Context.Events.Register(binding);

        return Task.FromResult(Data(binding.Id));
    }
}
