using App.Errors;
using App.Variables;

namespace App.modules.condition.providers;

public sealed class DefaultEvaluator : IEvaluator
{
    public string Name => "default";
    public bool IsDefault { get; set; }

    public Data.@this Evaluate(If action)
    {
        try
        {
            bool result = action.Operator.Evaluate(action.Left, action.Right);
            return App.Data.@this.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator, action.Right, ex);
        }
    }

    public Data.@this Evaluate(Compare action)
    {
        try
        {
            bool result = action.Operator.Evaluate(action.Left, action.Right);
            return App.Data.@this.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator, action.Right, ex);
        }
    }

    private static Data.@this EvaluationError(Data.@this? left, Operator op, Data.@this? right, Exception ex)
    {
        var leftType = left?.Value?.GetType().Name ?? "null";
        var rightType = right?.Value?.GetType().Name ?? "null";

        return App.Data.@this.FromError(new ValidationError(
            $"Condition evaluation failed: '{left?.Value}' ({leftType}) {op.Value} '{right?.Value}' ({rightType}) — {ex.Message}",
            "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = $"Valid operators: {string.Join(", ", Operator.ValidValues)}"
        });
    }
}
