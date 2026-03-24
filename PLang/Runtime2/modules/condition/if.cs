using PLang.Runtime2.Engine;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

/// <summary>
/// Evaluates a condition and branches execution.
/// When <see cref="Operator"/> is null, performs a truthy check on <see cref="Left"/>.
/// When <see cref="Operator"/> is set, evaluates Left op Right via <see cref="DefaultEvaluator"/>.
/// Branches to <see cref="GoalIfTrue"/>/<see cref="GoalIfFalse"/> when set (goal mode),
/// or signals sub-step execution via the <c>__condition__</c> MemoryStack key (sub-step mode).
/// </summary>
[Action("if")]
public partial class If : IContext
{
    /// <summary>The left operand (value or %variable%).</summary>
    public partial object? Left { get; init; }
    /// <summary>The comparison operator (null for truthy check). Case-insensitive.</summary>
    public partial string? Operator { get; init; }
    /// <summary>The right operand (null for unary operators like NOT, isEmpty).</summary>
    public partial object? Right { get; init; }
    /// <summary>Goal to call when the condition is true.</summary>
    public partial GoalCall? GoalIfTrue { get; init; }
    /// <summary>Goal to call when the condition is false.</summary>
    public partial GoalCall? GoalIfFalse { get; init; }

    /// <summary>
    /// Evaluates the condition and either calls a goal or returns a bool for sub-step control.
    /// Sets <c>__condition__</c> in MemoryStack so <c>Steps.RunAsync</c> can skip/execute indented children.
    /// Returns <see cref="Data"/> with error key "EvaluationError" on unsupported operators or type mismatches.
    /// </summary>
    public async Task<Data> Run()
    {
        var evaluatorResult = Context.Engine.Providers.Get<IEvaluator>();
        if (!evaluatorResult.Success) return evaluatorResult;
        var evaluator = evaluatorResult.Value!;

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
