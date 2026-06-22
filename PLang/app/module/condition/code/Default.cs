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
        data.@this<global::app.type.choice.@this<Operator>> operatorData, data.@this? left, data.@this? right)
    {
        if (!operatorData.Success || await operatorData.Value() == null)
            return global::app.data.@this<global::app.type.@bool.@this>.From(operatorData);
        try
        {
            // A condition is a boolean question — an absent variable operand is tolerated
            // (→ the operators' null-handling: ==→false, isempty→true, ordering→error),
            // NOT the loud value door. Null only an absent VARIABLE; a present value or a
            // lazy reference (file/url) is left untouched so its own door still fires.
            left = await TolerateAbsentVariable(left);
            right = await TolerateAbsentVariable(right);
            Operator op = (await operatorData.Value())!; bool result = await op.Evaluate(left, right);
            return global::app.data.@this<global::app.type.@bool.@this>.Ok(result);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(left, (await operatorData.Value())!, right, ex);
        }
    }

    /// <summary>
    /// A condition operand that is an unbound variable reference resolves to <c>null</c>
    /// (the operators handle a null operand by their own rules) rather than tripping the
    /// loud value door. Only an absent <c>variable</c> is nulled — a present value or a
    /// lazy reference passes through unchanged so its own door still resolves.
    /// </summary>
    private static async Task<data.@this?> TolerateAbsentVariable(data.@this? d)
    {
        if (d?.Peek() is global::app.variable.@this v && d.Context != null
            && !(await d.Context.Variable.Get(v.Name)).IsInitialized)
            return null;
        return d;
    }

    private static data.@this<global::app.type.@bool.@this> EvaluationError(data.@this? left, Operator op, data.@this? right, Exception ex)
    {
        var leftType = left?.Peek()?.GetType().Name ?? "null";
        var rightType = right?.Peek()?.GetType().Name ?? "null";

        return global::app.data.@this<global::app.type.@bool.@this>.FromError(new ValidationError(
            $"Condition evaluation failed: '{left?.Peek()}' ({leftType}) {op.Value} '{right?.Peek()}' ({rightType}) — {ex.Message}",
            "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = $"Valid operators: {string.Join(", ", Operator.Choices(null))}"
        });
    }
}
