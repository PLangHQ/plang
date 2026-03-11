using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

/// <summary>
/// Pure boolean evaluation — compares two values without branching.
/// Used as an intermediate step in compound conditions (AND/OR) where
/// sub-results from multiple <c>compare</c> actions feed into a final <c>if</c>.
/// Does NOT set <c>__condition__</c> — only <see cref="If"/> controls sub-step execution.
/// </summary>
[Action("compare")]
public partial class Compare : IContext
{
    /// <summary>The left operand (value or %variable%).</summary>
    public partial object? Left { get; init; }
    /// <summary>The comparison operator (required). Case-insensitive.</summary>
    public partial string Operator { get; init; }
    /// <summary>The right operand (null for unary operators).</summary>
    public partial object? Right { get; init; }

    /// <summary>
    /// Evaluates Left Operator Right and returns a bool wrapped in <see cref="Data"/>.
    /// Returns error key "EvaluationError" on unsupported operators or type mismatches.
    /// </summary>
    public Task<Data> Run()
    {
        var evaluator = new DefaultEvaluator();
        try
        {
            bool result = evaluator.Evaluate(Left, Operator, Right);
            return Task.FromResult(Data.Ok(result));
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
        {
            var leftType = Left?.GetType().Name ?? "null";
            var rightType = Right?.GetType().Name ?? "null";
            var message = $"Comparison failed: '{Left}' ({leftType}) {Operator} '{Right}' ({rightType}) — {ex.Message}";

            return Task.FromResult(Data.FromError(new ValidationError(message, Context, "EvaluationError")
            {
                Exception = ex,
                FixSuggestion = $"Check that operator '{Operator}' is supported (==, !=, >, <, >=, <=, contains, startswith, endswith, in, isempty, not, and, or) and that both operands are compatible types."
            }));
        }
    }
}
