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
    public partial Data.@this<string> Type { get; init; }
    /// <summary>Goal to execute when the event fires.</summary>
    public partial Data.@this<GoalCall> GoalToCall { get; init; }
    /// <summary>Glob or regex pattern to match goal names. Null matches all goals.</summary>
    public partial Data.@this<string>? GoalPattern { get; init; }
    /// <summary>Glob or regex pattern to match step text. Only for step-level events.</summary>
    public partial Data.@this<string>? StepPattern { get; init; }
    /// <summary>Glob or regex pattern to match action names (e.g., "http.*"). Only for action-level events.</summary>
    public partial Data.@this<string>? ActionPattern { get; init; }
    /// <summary>When true, patterns are treated as regular expressions instead of glob patterns.</summary>
    [Default(false)]
    public partial Data.@this<bool> IsRegex { get; init; }
    /// <summary>Execution priority — higher values run first. Default is 0.</summary>
    [Default(0)]
    public partial Data.@this<int> Priority { get; init; }

    /// <summary>Actor to bind the event to. If null, uses current actor.</summary>
    public partial Actor.@this? Actor { get; init; }

    public Task<Data.@this> Run()
    {
        if (!Enum.TryParse<EventType>(Type.Value!, ignoreCase: true, out var eventType))
            return Task.FromResult(Error(
                new Errors.ValidationError($"Unknown event type: '{Type.Value}'", "InvalidEventType", 400)));

        // Resolve target actor — default to current context's actor
        var targetActor = Actor ?? Context.Actor ?? Context.App!.User;

        var goalToCall = GoalToCall.Value!;
        Func<Actor.Context.@this, Task<Data.@this>> handler = async ctx =>
            await ctx.App!.RunGoalAsync(goalToCall, targetActor.Context, ctx.CancellationToken);

        var binding = new EventBinding(
            eventType,
            handler,
            goalNamePattern: GoalPattern?.Value,
            stepPattern: StepPattern?.Value,
            actionPattern: ActionPattern?.Value,
            priority: Priority.Value,
            isRegex: IsRegex.Value,
            goalToCall: goalToCall);

        // Register on the target actor's event scope
        targetActor.Context.Events.Register(binding);

        return Task.FromResult(Data(binding.Id));
    }
}
