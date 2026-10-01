using System.Threading.Tasks;
using app.error;
using Operator = global::app.data.Operator;

namespace app.module.condition.code;

public sealed class Default : IEvaluator
{
    public string Name => "default";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // A property left out is born uninitialized; a written Right=null is initialized, holding null.
    // A condition with no Operator is Left's own truth (Operator.Truth) — the value answers for itself (a
    // path: does it exist). What doesn't exist is false; a failed Left is its error.

    public Task<data.@this<global::app.type.item.@bool.@this>> Evaluate(If action) =>
        action.Operator is { IsInitialized: true } op ? EvaluateOperator(op, action.Left, action.Right) : Operator.Truth(action.Left, action.Context);

    public Task<data.@this<global::app.type.item.@bool.@this>> Evaluate(Elseif action) =>
        action.Operator is { IsInitialized: true } op ? EvaluateOperator(op, action.Left, action.Right) : Operator.Truth(action.Left, action.Context);

    public async Task<global::app.error.Error?> Operands(data.@this<global::app.type.item.choice.@this<Operator>>? op, data.@this? right)
    {
        var hasRight = right is { IsInitialized: true };
        if (op is not { IsInitialized: true })
            return !hasRight ? null : new ValidationError(
                "Right is compared by an Operator — write the Operator, or leave Right out for Left's own truth", "OperandExtra");
        if (op.HasVariable) return null;   // an operator named by a variable is known at run
        Operator written = (await op.Value())!;
        return written.Operands(hasRight);
    }

    public Task<data.@this<global::app.type.item.@bool.@this>> Evaluate(Compare action) =>
        EvaluateOperator(action.Operator, action.Left, action.Right);

    /// <summary>
    /// Shared evaluation core for If / Elseif / Compare. The three actions
    /// only differ in their declaring type — Operator, Left, Right have
    /// identical semantics, and the guard + try/catch is identical.
    /// </summary>
    private static async Task<data.@this<global::app.type.item.@bool.@this>> EvaluateOperator(
        data.@this<global::app.type.item.choice.@this<Operator>> operatorData, data.@this? left, data.@this? right)
    {
        if (!operatorData.Success || await operatorData.Value() == null)
            return global::app.data.@this<global::app.type.item.@bool.@this>.From(operatorData);
        try
        {
            // The operator answers a plang bool — true, false, or the developer's error. Each operator
            // reads its operands as it asks: truthiness and equality read a reference to nothing as null,
            // ordering refuses it (its VariableNotFound is the answer).
            Operator op = (await operatorData.Value())!;
            return await op.Evaluate(left, right, operatorData.Context);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or InvalidCastException)
        {
            return EvaluationError(operatorData.Context, left, (await operatorData.Value())!, right, ex);
        }
    }

    private static data.@this<global::app.type.item.@bool.@this> EvaluationError(global::app.actor.context.@this ctx, data.@this? left, Operator op, data.@this? right, Exception ex)
    {
        var leftType = left?.Peek()?.GetType().Name ?? "null";
        var rightType = right?.Peek()?.GetType().Name ?? "null";

        return ctx.Error<global::app.type.item.@bool.@this>(new ValidationError(
            $"Condition evaluation failed: '{left?.Peek()}' ({leftType}) {op.Value} '{right?.Peek()}' ({rightType}) — {ex.Message}",
            "EvaluationError")
        {
            Exception = ex,
            FixSuggestion = $"Valid operators: {string.Join(", ", Operator.Choices(null))}"
        });
    }
}
