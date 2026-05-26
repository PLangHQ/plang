using System.Threading.Tasks;
using app.errors;
using app.variables;

namespace app.modules.condition.code;

public sealed class Default : IEvaluator
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public async Task<data.@this<bool>> Evaluate(If action)
    {
        if (!action.Operator.Success || action.Operator.Value == null)
            return global::app.data.@this<bool>.From(action.Operator);
        try
        {
            bool result = await action.Operator.Value.Evaluate(action.Left, action.Right);
            return global::app.data.@this<bool>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator.Value, action.Right, ex);
        }
    }

    public async Task<data.@this<bool>> Evaluate(Elseif action)
    {
        if (!action.Operator.Success || action.Operator.Value == null)
            return global::app.data.@this<bool>.From(action.Operator);
        try
        {
            bool result = await action.Operator.Value.Evaluate(action.Left, action.Right);
            return global::app.data.@this<bool>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator.Value, action.Right, ex);
        }
    }

    public async Task<data.@this<bool>> Evaluate(Compare action)
    {
        if (!action.Operator.Success || action.Operator.Value == null)
            return global::app.data.@this<bool>.From(action.Operator);
        try
        {
            bool result = await action.Operator.Value.Evaluate(action.Left, action.Right);
            return global::app.data.@this<bool>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(action.Left, action.Operator.Value, action.Right, ex);
        }
    }

    private static data.@this<bool> EvaluationError(data.@this? left, Operator op, data.@this? right, Exception ex)
    {
        var leftType = left?.Value?.GetType().Name ?? "null";
        var rightType = right?.Value?.GetType().Name ?? "null";

        return global::app.data.@this<bool>.FromError(new ValidationError(
            $"Condition evaluation failed: '{left?.Value}' ({leftType}) {op.Value} '{right?.Value}' ({rightType}) — {ex.Message}",
            "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = $"Valid operators: {string.Join(", ", Operator.Choices(null))}"
        });
    }
}
