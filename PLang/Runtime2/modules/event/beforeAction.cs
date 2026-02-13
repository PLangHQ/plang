using PLang.Runtime2.Core;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.@event;

[Action("beforeAction", Cacheable = false)]
public partial class BeforeAction : IContext
{
    public partial GoalCall GoalToCall { get; init; }
    public partial string? ActionPattern { get; init; }
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
            EventType.BeforeAction,
            handler,
            actionPattern: ActionPattern,
            priority: Priority,
            isRegex: IsRegex);

        Context.User.Events.Register(binding);

        return Task.FromResult(Data.Ok(new types.@event
        {
            id = binding.Id,
            type = "beforeAction",
            goalToCall = GoalToCall.Name,
            pattern = ActionPattern,
            isRegex = IsRegex
        }));
    }
}
