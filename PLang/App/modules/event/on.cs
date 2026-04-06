using App;
using App.Variables;
using App.Events;
using EventBinding = App.Events.Lifecycle.Bindings.Binding.@this;

namespace App.modules.@event;

/// <summary>
/// Registers an event binding on the execution lifecycle.
/// Consolidates all event types (BeforeGoal, AfterGoal, BeforeStep, AfterStep, BeforeAction, AfterAction)
/// into a single action with a Type parameter. Returns the binding ID for later removal.
/// </summary>
[Example("before step, call LogStep, on goal pattern 'Api/*'", "Type=BeforeStep, GoalToCall=LogStep, GoalPattern=Api/*")]
[Example("after goal, call Cleanup", "Type=AfterGoal, GoalToCall=Cleanup")]
[Example("before action, call MockHttp, on action pattern 'http.*'", "Type=BeforeAction, GoalToCall=MockHttp, ActionPattern=http.*")]
[Example("on before goal, call AuthCheck, on goal pattern '^Admin', is regex", "Type=BeforeGoal, GoalToCall=AuthCheck, GoalPattern=^Admin, IsRegex=true")]
[Action("on", Cacheable = false)]
public partial class On : IContext
{
    /// <summary>Event type: BeforeGoal, AfterGoal, BeforeStep, AfterStep, BeforeAction, AfterAction.</summary>
    [IsNotNull]
    public partial string Type { get; init; }
    /// <summary>Goal to execute when the event fires.</summary>
    public partial GoalCall GoalToCall { get; init; }
    /// <summary>Glob or regex pattern to match goal names. Null matches all goals.</summary>
    public partial string? GoalPattern { get; init; }
    /// <summary>Glob or regex pattern to match step text. Only for step-level events.</summary>
    public partial string? StepPattern { get; init; }
    /// <summary>Glob or regex pattern to match action names (e.g., "http.*"). Only for action-level events.</summary>
    public partial string? ActionPattern { get; init; }
    /// <summary>When true, patterns are treated as regular expressions instead of glob patterns.</summary>
    [Default(false)]
    public partial bool IsRegex { get; init; }
    /// <summary>Execution priority — higher values run first. Default is 0.</summary>
    [Default(0)]
    public partial int Priority { get; init; }

    /// <summary>Actor to bind the event to. If null, uses current actor.</summary>
    public partial Context.Actor? Actor { get; init; }

    public Task<Data> Run()
    {
        if (!Enum.TryParse<EventType>(Type, ignoreCase: true, out var eventType))
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Unknown event type: '{Type}'", "InvalidEventType", 400)));

        // Resolve target actor — default to current context's actor
        var targetActor = Actor ?? Context.Actor ?? Context.Engine!.User;

        Func<Context.@this, Task<Data>> handler = async ctx =>
            await ctx.Engine!.RunGoalAsync(GoalToCall, targetActor.Context, ctx.CancellationToken);

        var binding = new EventBinding(
            eventType,
            handler,
            goalNamePattern: GoalPattern,
            stepPattern: StepPattern,
            actionPattern: ActionPattern,
            priority: Priority,
            isRegex: IsRegex,
            goalToCall: GoalToCall);

        // Register on the target actor's event scope
        targetActor.Context.Events.Register(binding);

        return Task.FromResult(Data.Ok(binding.Id));
    }
}
