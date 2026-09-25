using app;
using app.variable;
using app.module.action.condition.code;

namespace app.module.action.condition;

[Action("elseif")]
public partial class Elseif : IContext, IStep
{
    public partial data.@this? Left { get; init; }
    public partial data.@this<global::app.type.item.choice.@this<Operator>> Operator { get; init; }
    public partial data.@this? Right { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    public async Task<data.@this> Run()
    {
        var evalResult = await Evaluator.Evaluate(this);
        if (!evalResult.Success) return evalResult;
        // The truthiness door — the value answers for itself; a negative question is its operator's.
        return Data(await evalResult.ToBooleanAsync());
    }
}
