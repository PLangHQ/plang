using App.Engine.Errors;
using App.Engine.Variables;

namespace App.modules.condition.providers;

public sealed class DefaultEvaluator : IEvaluator
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data Evaluate(If action)
    {
        try
        {
            bool result = action.Operator.Evaluate(action.Left, action.Right);
            return Data.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator, action.Right, ex);
        }
    }

    public Data Evaluate(Compare action)
    {
        try
        {
            bool result = action.Operator.Evaluate(action.Left, action.Right);
            return Data.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator, action.Right, ex);
        }
    }

    private static Data EvaluationError(Data? left, Operator op, Data? right, Exception ex)
    {
        var leftType = left?.Value?.GetType().Name ?? "null";
        var rightType = right?.Value?.GetType().Name ?? "null";

        return Data.FromError(new ValidationError(
            $"Condition evaluation failed: '{left?.Value}' ({leftType}) {op.Value} '{right?.Value}' ({rightType}) — {ex.Message}",
            "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = $"Valid operators: {string.Join(", ", Operator.ValidValues)}"
        });
    }
}
