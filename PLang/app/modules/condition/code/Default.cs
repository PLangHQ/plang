using app.Errors;
using app.Variables;

namespace app.modules.condition.code;

public sealed class Default : IEvaluator
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public data.@this Evaluate(If action)
    {
        try
        {
            bool result = action.Operator.Value.Evaluate(action.Left, action.Right);
            return global::app.data.@this.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator.Value, action.Right, ex);
        }
    }

    public data.@this Evaluate(Elseif action)
    {
        try
        {
            bool result = action.Operator.Value.Evaluate(action.Left, action.Right);
            return global::app.data.@this.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator.Value, action.Right, ex);
        }
    }

    public data.@this Evaluate(Compare action)
    {
        try
        {
            bool result = action.Operator.Value.Evaluate(action.Left, action.Right);
            return global::app.data.@this.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator.Value, action.Right, ex);
        }
    }

    private static data.@this EvaluationError(data.@this? left, Operator op, data.@this? right, Exception ex)
    {
        var leftType = left?.Value?.GetType().Name ?? "null";
        var rightType = right?.Value?.GetType().Name ?? "null";

        return global::app.data.@this.FromError(new ValidationError(
            $"Condition evaluation failed: '{left?.Value}' ({leftType}) {op.Value} '{right?.Value}' ({rightType}) — {ex.Message}",
            "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = $"Valid operators: {string.Join(", ", Operator.Choices(null))}"
        });
    }
}
