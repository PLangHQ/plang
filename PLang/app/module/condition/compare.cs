using app.variable;
using app.module.condition.code;

namespace app.module.condition;

[Action("compare")]
public partial class Compare : IContext
{
    public partial data.@this? Left { get; init; }
    public partial data.@this<Operator> Operator { get; init; }
    public partial data.@this? Right { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    public Task<data.@this<bool>> Run() => Evaluator.Evaluate(this);
}
