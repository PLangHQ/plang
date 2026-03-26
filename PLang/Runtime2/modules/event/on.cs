using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Events;
using EventBinding = PLang.Runtime2.Engine.Events.Lifecycle.Bindings.Binding.@this;

namespace PLang.Runtime2.modules.@event;

[Action("on", Cacheable = false)]
public partial class On : IContext
{
    [IsNotNull]
    public partial string Type { get; init; }
    public partial GoalCall GoalToCall { get; init; }
    public partial string? GoalPattern { get; init; }
    public partial string? StepPattern { get; init; }
    public partial string? ActionPattern { get; init; }
    [Default(false)]
    public partial bool IsRegex { get; init; }
    [Default(0)]
    public partial int Priority { get; init; }

    public Task<Data> Run()
    {
        if (!Enum.TryParse<EventType>(Type, ignoreCase: true, out var eventType))
            return Task.FromResult(Data.FromError(
                new Engine.Errors.ValidationError($"Unknown event type: '{Type}'", "InvalidEventType", 400)));

        Func<Engine.Context.PLangContext, Task<Data>> handler = async ctx =>
            await ctx.Engine!.RunGoalAsync(GoalToCall, ctx, ctx.CancellationToken);

        var binding = new EventBinding(
            eventType,
            handler,
            goalNamePattern: GoalPattern,
            stepPattern: StepPattern,
            actionPattern: ActionPattern,
            priority: Priority,
            isRegex: IsRegex);

        Context.User.Events.Register(binding);

        return Task.FromResult(Data.Ok(binding.Id));
    }
}
