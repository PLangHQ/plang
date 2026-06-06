using System.Threading.Tasks;
using app.error;
using app.variable;

namespace app.module.condition.code;

public sealed class Default : IEvaluator
{
    public string Name => "default";
    public bool IsDefault { get; set; }
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    public Task<data.@this<global::app.type.@bool.@this>> Evaluate(If action) =>
        EvaluateOperator(action.Operator, action.Left, action.Right);

    public Task<data.@this<global::app.type.@bool.@this>> Evaluate(Elseif action) =>
        EvaluateOperator(action.Operator, action.Left, action.Right);

    public Task<data.@this<global::app.type.@bool.@this>> Evaluate(Compare action) =>
        EvaluateOperator(action.Operator, action.Left, action.Right);

    /// <summary>
    /// Shared evaluation core for If / Elseif / Compare. The three actions
    /// only differ in their declaring type — Operator, Left, Right have
    /// identical semantics, and the guard + try/catch is identical.
    /// </summary>
    private static async Task<data.@this<global::app.type.@bool.@this>> EvaluateOperator(
        data.@this<Operator> operatorData, data.@this? left, data.@this? right)
    {
        if (!operatorData.Success || operatorData.Value == null)
            return global::app.data.@this<global::app.type.@bool.@this>.From(operatorData);
        try
        {
            bool result = await operatorData.Value.Evaluate(left, right);
            return global::app.data.@this<global::app.type.@bool.@this>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(left, operatorData.Value, right, ex);
        }
    }

    private static data.@this<global::app.type.@bool.@this> EvaluationError(data.@this? left, Operator op, data.@this? right, Exception ex)
    {
        var leftType = left?.Value?.GetType().Name ?? "null";
        var rightType = right?.Value?.GetType().Name ?? "null";

        return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ValidationError(
            $"Condition evaluation failed: '{left?.Value}' ({leftType}) {op.Value} '{right?.Value}' ({rightType}) — {ex.Message}",
            "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = $"Valid operators: {string.Join(", ", Operator.Choices(null))}"
        });
    }
}
