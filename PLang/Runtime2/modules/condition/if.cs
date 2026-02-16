using PLang.Runtime2;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.condition;

[Action("if")]
public partial class If : IContext
{
    public partial bool Condition { get; init; }
    public partial GoalCall? GoalIfTrue { get; init; }
    public partial GoalCall? GoalIfFalse { get; init; }

    public async Task<Data> Run()
    {
        GoalCall? goalToCall = Condition ? GoalIfTrue : GoalIfFalse;

        if (goalToCall != null)
        {
            var result = await Context.Engine!.RunGoalAsync(goalToCall, Context, Context.CancellationToken);
            if (!result.Success) return result;
        }

        return Data.Ok(Condition);
    }
}
