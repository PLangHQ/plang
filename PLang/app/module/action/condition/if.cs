using app;
using app.variable;
using app.module.action.condition.code;

namespace app.module.action.condition;

[Action("if")]
public partial class If : IContext, IStep
{
    public partial data.@this Left { get; init; }
    /// <summary>Left out: the condition is Left's own truth — is it true, does it exist, is it set.</summary>
    public partial data.@this<global::app.type.item.choice.@this<Operator>>? Operator { get; init; }
    public partial data.@this? Right { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    /// <summary>The operands read as the operator asks: a Right for a comparison, none for emptiness,
    /// and none without an Operator.</summary>
    public async Task<global::app.error.Error?> Validate() => await Evaluator.Operands(Operator, Right);

    /// <summary>Evaluate-only: the condition answers its own truthiness — a negative question is its
    /// operator's (notcontains, isnotempty, …). The chain — which branch fires, running its Child,
    /// skipping the rest — is owned by <c>action.list.Run</c>; a condition no longer reaches its
    /// siblings or its Step.</summary>
    public async Task<data.@this> Run()
    {
        var evalResult = await Evaluator.Evaluate(this);
        if (!evalResult.Success) return evalResult;

        // The truthiness door — the value answers for itself.
        return Data(await evalResult.ToBooleanAsync());
    }
}
