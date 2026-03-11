using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

[Action("if")]
public partial class If : IContext
{
    public partial object? Left { get; init; }
    public partial string? Operator { get; init; }
    public partial object? Right { get; init; }
    public partial GoalCall? GoalIfTrue { get; init; }
    public partial GoalCall? GoalIfFalse { get; init; }

    public async Task<Data> Run()
    {
        var evaluator = new DefaultEvaluator();

        bool result = Operator == null
            ? evaluator.IsTruthy(Left)
            : evaluator.Evaluate(Left, Operator, Right);

        // Store condition outcome so Steps.RunAsync can check it for sub-step control.
        // Data.Merge (used by Actions.RunAsync) loses the bool Value, so we signal via memory.
        Context.MemoryStack.Set("__condition__", result);

        GoalCall? goalToCall = result ? GoalIfTrue : GoalIfFalse;

        if (goalToCall != null)
        {
            var goalResult = await Context.Engine!.RunGoalAsync(goalToCall, Context, Context.CancellationToken);
            if (!goalResult.Success) return goalResult;
        }

        return Data.Ok(result);
    }
}
