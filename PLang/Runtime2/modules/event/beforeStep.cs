using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.@event;

[Action("beforeStep")]
public partial class BeforeStep : IContext
{
    public partial string GoalToCall { get; init; }
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
            goalToCall = GoalToCall,
            pattern = GoalPattern ?? StepPattern,
            isRegex = IsRegex
        }));
    }
}
