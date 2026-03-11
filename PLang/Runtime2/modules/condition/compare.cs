using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.condition.providers;

namespace PLang.Runtime2.modules.condition;

[Action("compare")]
public partial class Compare : IContext
{
    public partial object? Left { get; init; }
    public partial string Operator { get; init; }
    public partial object? Right { get; init; }

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
