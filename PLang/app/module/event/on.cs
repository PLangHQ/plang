using app;
using app.variable;
using app.@event;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.module.@event;

/// <summary>
/// Registers an event binding on the execution lifecycle.
/// Consolidates all lifecycle moments into a single action with a Trigger parameter
/// (the `trigger` enum). Returns the binding ID for later removal.
/// </summary>
[Action("on", Cacheable = false)]
public partial class On : IContext
{
    /// <summary>Lifecycle moment the callback binds to (a <c>trigger</c> enum value, e.g. BeforeGoal, OnAsk).</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<Trigger>> Trigger { get; init; }
    /// <summary>Goal to execute when the event fires.</summary>
    public partial data.@this<GoalCall> GoalToCall { get; init; }
    /// <summary>Glob or regex pattern to match goal names. Null matches all goals.</summary>
    public partial data.@this<global::app.type.item.text.@this>? GoalPattern { get; init; }
    /// <summary>Glob or regex pattern to match step text. Only for step-level events.</summary>
    public partial data.@this<global::app.type.item.text.@this>? StepPattern { get; init; }
    /// <summary>Glob or regex pattern to match action names (e.g., "http.*"). Only for action-level events.</summary>
    public partial data.@this<global::app.type.item.text.@this>? ActionPattern { get; init; }
    /// <summary>When true, patterns are treated as regular expressions instead of glob patterns.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> IsRegex { get; init; }
    /// <summary>Execution priority — higher values run first. Default is 0.</summary>
    [Default(0)]
    public partial data.@this<global::app.type.number.@this> Priority { get; init; }

    /// <summary>Actor to bind the event to. If null, uses current actor.</summary>
    public partial data.@this<actor.@this>? Actor { get; init; }

    /// <summary>Channel-name filter for channel lifecycle events (BeforeWrite/AfterWrite/BeforeRead/AfterRead/OnAsk). Null = no filter.</summary>
    public partial data.@this<global::app.type.item.text.@this>? ChannelName { get; init; }

    public async Task<data.@this<global::app.type.item.text.@this>> Run()
    {
        // Resolve target actor — default to current context's actor
        var targetActor = (Actor == null ? null : await Actor.Value()) ?? Context.Actor ?? Context.App.User;

        var goalToCall = (await GoalToCall.Value())!;
        Func<actor.context.@this, global::app.goal.steps.step.actions.action.@this?, data.@this?, Task<data.@this>> handler =
            async (context, _, _) => await context.App!.RunGoalAsync(goalToCall, targetActor.Context, context.CancellationToken);

        var binding = new EventBinding(
            await Trigger.Value(),
            handler,
            goalNamePattern: GoalPattern == null ? null : (await GoalPattern.Value())?.Clr<string>(),
            stepPattern: StepPattern == null ? null : (await StepPattern.Value())?.Clr<string>(),
            actionPattern: ActionPattern == null ? null : (await ActionPattern.Value())?.Clr<string>(),
            priority: (await Priority.Value())!.ToInt32(),
            isRegex: (await IsRegex.Value())!.Value,
            goalToCall: goalToCall,
            channelName: ChannelName == null ? null : (await ChannelName.Value())?.Clr<string>());

        // Register on the target actor's event scope
        targetActor.Context.Events.Register(binding);

        return Context.Ok<global::app.type.item.text.@this>(binding.Id);
    }
}
