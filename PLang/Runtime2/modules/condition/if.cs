using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Errors;
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

        bool result;
        try
        {
            result = Operator == null
                ? evaluator.IsTruthy(Left)
                : evaluator.Evaluate(Left, Operator, Right);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
        {
            return Data.FromError(EvaluationError(ex));
        }

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

    private ValidationError EvaluationError(Exception ex)
    {
        var leftType = Left?.GetType().Name ?? "null";
        var rightType = Right?.GetType().Name ?? "null";
        var message = Operator != null
            ? $"Condition evaluation failed: '{Left}' ({leftType}) {Operator} '{Right}' ({rightType}) — {ex.Message}"
            : $"Condition evaluation failed: IsTruthy('{Left}' ({leftType})) — {ex.Message}";

        return new ValidationError(message, Context, "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = Operator != null
                ? $"Check that operator '{Operator}' is supported (==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, not, and, or) and that both operands are compatible types."
                : "Check that the left operand is a type that can be evaluated for truthiness."
        };
    }
}
