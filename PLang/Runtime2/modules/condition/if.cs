using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.condition;

[Action("if")]
public partial class If : IContext
{
    public partial bool Condition { get; init; }
    public partial string? GoalIfTrue { get; init; }
    public partial string? GoalIfFalse { get; init; }

    public async Task<Data> Run()
    {
        string? goalToCall = Condition ? GoalIfTrue : GoalIfFalse;

        if (goalToCall != null)
        {
            var result = await Context.Engine!.RunGoalAsync(goalToCall, Context, Context.CancellationToken);
            if (!result.Success) return result;
        }

        return Data.Ok(Condition);
    }
}
