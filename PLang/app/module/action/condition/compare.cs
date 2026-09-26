using app.variable;
using app.module.action.condition.code;

namespace app.module.action.condition;

[Action("compare")]
public partial class Compare : IContext
{
    public partial data.@this? Left { get; init; }
    public partial data.@this<global::app.type.item.choice.@this<Operator>> Operator { get; init; }
    public partial data.@this? Right { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    /// <summary>The operands read as the operator asks.</summary>
    public async System.Threading.Tasks.Task<global::app.error.Error?> Validate() => await Evaluator.Operands(Operator, Right);

    public Task<data.@this<global::app.type.item.@bool.@this>> Run() => Evaluator.Evaluate(this);
}
