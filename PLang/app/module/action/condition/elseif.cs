using app;
using app.module.action.condition.code;
using Operator = global::app.data.Operator;

namespace app.module.action.condition;

[Action("elseif")]
public partial class Elseif : IContext, IStep
{
    public partial data.@this Left { get; init; }
    /// <summary>Left out: the condition is Left's own truth — is it true, does it exist, is it set.</summary>
    public partial data.@this<global::app.type.item.choice.@this<Operator>>? Operator { get; init; }
    public partial data.@this? Right { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    /// <summary>The operands read as the operator asks.</summary>
    public async Task<global::app.error.Error?> Validate() => await Evaluator.Operands(Operator, Right);

    /// <summary>The evaluator's plang bool is the answer, as it is (see <see cref="If.Start"/>).</summary>
    public Task<data.@this<global::app.type.item.@bool.@this>> Start() => Evaluator.Evaluate(this);
}
