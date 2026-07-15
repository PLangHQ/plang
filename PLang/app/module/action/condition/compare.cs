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

    public Task<data.@this<global::app.type.item.@bool.@this>> Run() => Evaluator.Evaluate(this);
}
