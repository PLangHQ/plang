using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

[Action("if")]
public partial class If : IContext
{
    public partial Data? Left { get; init; }
    public partial string? Operator { get; init; }
    public partial Data? Right { get; init; }
    public partial GoalCall? GoalIfTrue { get; init; }
    public partial GoalCall? GoalIfFalse { get; init; }

    [Provider]
    public partial IEvaluator Evaluator { get; }

    public async Task<Data> Run()
    {
        var evalResult = Evaluator.Evaluate(this);
        if (!evalResult.Success) return evalResult;

        var goalToCall = evalResult.Value is true ? GoalIfTrue : GoalIfFalse;
        if (goalToCall != null)
        {
            var goalResult = await Context.Engine!.RunGoalAsync(goalToCall, Context, Context.CancellationToken);
            if (!goalResult.Success) return goalResult;
        }

        return evalResult;
    }
}
