using app;
using app.module.action.condition.code;
using Operator = global::app.data.Operator;

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

    /// <summary>Evaluate-only: the evaluator's plang bool is the answer, as it is — the operator decided
    /// it, or Left's own truth (the value answers for itself); a negative question is its operator's. The
    /// chain — which branch fires, running its Child, skipping the rest — is <c>action.list.Start</c>'s.</summary>
    public Task<data.@this<global::app.type.item.@bool.@this>> Start() => Evaluator.Evaluate(this);
}
