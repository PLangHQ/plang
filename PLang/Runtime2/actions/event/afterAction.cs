using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Events;

namespace PLang.Runtime2.actions.@event;

[Action("afterAction", Cacheable = false)]
public partial class AfterAction : IContext
{
    public partial GoalCall GoalToCall { get; init; }
    public partial string? ActionPattern { get; init; }
    [Default(false)]
    public partial bool IsRegex { get; init; }
    [Default(0)]
    public partial int Priority { get; init; }

    public Task<Data> Run()
    {
        Func<Engine.Context.PLangContext, Task<Data>> handler = async ctx =>
        {
            return await ctx.Engine!.RunGoalAsync(GoalToCall, ctx, ctx.CancellationToken);
        };

        var binding = new EventBinding(
            EventType.AfterAction,
            handler,
            actionPattern: ActionPattern,
            priority: Priority,
            isRegex: IsRegex);

        Context.User.Events.Register(binding);

        return Task.FromResult(Data.Ok(new types.@event
        {
            id = binding.Id,
            type = "afterAction",
            goalToCall = GoalToCall.Name,
            pattern = ActionPattern,
            isRegex = IsRegex
        }));
    }
}
