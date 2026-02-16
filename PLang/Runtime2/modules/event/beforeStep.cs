using PLang.Runtime2;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.@event;

[Action("beforeStep", Cacheable = false)]
public partial class BeforeStep : IContext
{
    public partial GoalCall GoalToCall { get; init; }
    public partial string? GoalPattern { get; init; }
    public partial string? StepPattern { get; init; }
    [Default(false)]
    public partial bool IsRegex { get; init; }
    [Default(0)]
    public partial int Priority { get; init; }

    public Task<Data> Run()
    {
        Func<Context.PLangContext, Task<Data>> handler = async ctx =>
        {
            return await ctx.Engine!.RunGoalAsync(GoalToCall, ctx, ctx.CancellationToken);
        };

        var binding = new EventBinding(
            EventType.BeforeStep,
            handler,
            goalNamePattern: GoalPattern,
            stepPattern: StepPattern,
            priority: Priority,
            isRegex: IsRegex);

        Context.User.Events.Register(binding);

        return Task.FromResult(Data.Ok(new types.@event
        {
            id = binding.Id,
            type = "beforeStep",
            goalToCall = GoalToCall.Name,
            pattern = GoalPattern ?? StepPattern,
            isRegex = IsRegex
        }));
    }
}
