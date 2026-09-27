using app;
using app.@event;
using EventBinding = app.@event.lifecycle.binding.@this;

namespace app.module.action.@event;

/// <summary>
/// Registers an event binding on the execution lifecycle.
/// Consolidates all lifecycle moments into a single action with a Trigger parameter
/// (the `trigger` enum). Returns the binding ID for later removal.
/// A goal, step or action trigger binds on that type's <c>on.start</c> (before or after), for the target actor,
/// filtered by the patterns; a channel trigger registers on the actor's events.
/// </summary>
[Action("on", Cacheable = false)]
public partial class On : IContext
{
    /// <summary>Lifecycle moment the callback binds to (a <c>trigger</c> enum value, e.g. BeforeGoal, OnAsk).</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<Trigger>> Trigger { get; init; }
    /// <summary>The call to run when the event fires — a <c>goal.call</c> action, held whole and
    /// run as itself (its own step, arguments and modifiers).</summary>
    public partial data.@this<global::app.goal.step.action.@this> Goal { get; init; }
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
    public partial data.@this<global::app.type.item.number.@this> Priority { get; init; }

    /// <summary>The actor to bind the event to, by name. If null, uses the current actor.</summary>
    public partial data.@this<global::app.type.item.choice.@this<actor.Name>>? Actor { get; init; }

    /// <summary>Channel-name filter for channel lifecycle events (BeforeWrite/AfterWrite/BeforeRead/AfterRead/OnAsk). Null = no filter.</summary>
    public partial data.@this<global::app.type.item.text.@this>? ChannelName { get; init; }

    public async Task<data.@this<global::app.type.item.text.@this>> Start()
    {
        // Resolve target actor — default to current context's actor
        var named = Actor == null ? null : await Actor.Value();
        var targetActor = (named == null ? null : await (await Context.App.actor.Get(named.ToString()!)).Value())
                          ?? Context.Actor ?? Context.App.User;

        // The binding sets %!event% (the moment that fired) before the handler runs the held call.
        var call = (await Goal.Value())!;
        Func<actor.context.@this, global::app.goal.step.action.@this?, data.@this?, Task<data.@this>> handler =
            async (_, _, _) => await call.Start(targetActor.Context);

        Trigger trigger = await Trigger.Value();
        var binding = new EventBinding(
            trigger,
            handler,
            goalNamePattern: GoalPattern == null ? null : (await GoalPattern.Value())?.Clr<string>(),
            stepPattern: StepPattern == null ? null : (await StepPattern.Value())?.Clr<string>(),
            actionPattern: ActionPattern == null ? null : (await ActionPattern.Value())?.Clr<string>(),
            priority: (await Priority.Value())!.ToInt32(),
            isRegex: (await IsRegex.Value())!.Value,
            call: call,
            channelName: ChannelName == null ? null : (await ChannelName.Value())?.Clr<string>());

        // Registered on the target actor's events: a channel trigger fires from there; for the others it is
        // the id event.remove finds, holding the on.start binding it stands for.
        targetActor.Context.Events.Register(binding);

        if (Target(trigger) is { } at)
        {
            var bound = Context.App.type.list[at.Type].Own().Bind("start", at.When,
                async (item, result, context) =>
                {
                    await context.Variable.Set("!event", Moment(trigger, item, result));
                    return await call.Start(targetActor.Context);
                },
                targetActor, global::app.@event.binding.Scope.actor,
                (item, _) => Matches(binding, item));
            binding.Targets.Add(bound);
        }

        return Context.Ok<global::app.type.item.text.@this>(binding.Id);
    }

    // The type whose on.start a goal, step or action trigger binds on, and the side; null for the rest.
    private (string Type, When When)? Target(Trigger trigger) => trigger switch
    {
        global::app.@event.Trigger.BeforeGoal or global::app.@event.Trigger.OnBeforeGoalLoad => ("goal", When.before),
        global::app.@event.Trigger.AfterGoal or global::app.@event.Trigger.OnAfterGoalLoad => ("goal", When.after),
        global::app.@event.Trigger.BeforeStep or global::app.@event.Trigger.OnBeforeStepLoad => ("step", When.before),
        global::app.@event.Trigger.AfterStep or global::app.@event.Trigger.OnAfterStepLoad => ("step", When.after),
        global::app.@event.Trigger.BeforeAction => ("action", When.before),
        global::app.@event.Trigger.AfterAction => ("action", When.after),
        _ => null,
    };

    // Whether the patterns take the item: a goal by its name, a step by its goal's name and its text, an action
    // by module.action.
    private bool Matches(EventBinding patterns, global::app.type.item.@this item) => item switch
    {
        global::app.goal.@this goal => patterns.MatchesGoal(goal.Name),
        global::app.goal.step.@this step => (step.Goal?.Name is not { } name || patterns.MatchesGoal(name)) && patterns.MatchesStep(step.Text),
        global::app.goal.step.action.@this action => patterns.MatchesAction(action.Module.Name, action.Name),
        _ => false,
    };

    // What fired, as %!event% reads it: the goal, step or action, and an action's result after it.
    private global::app.@event.moment.@this Moment(Trigger trigger, global::app.type.item.@this item, data.@this result) => item switch
    {
        global::app.goal.@this goal => new(trigger, goal),
        global::app.goal.step.@this step => new(trigger, step),
        _ => new(trigger, (global::app.goal.step.action.@this)item, trigger == global::app.@event.Trigger.AfterAction ? result : null),
    };
}
