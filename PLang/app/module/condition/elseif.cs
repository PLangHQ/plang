using app;
using app.variable;
using app.module.condition.code;

namespace app.module.condition;

[Action("elseif")]
public partial class Elseif : IContext, IStep
{
    public partial data.@this? Left { get; init; }
    public partial data.@this<global::app.type.choice.@this<Operator>> Operator { get; init; }
    public partial data.@this? Right { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> Negate { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    public async Task<data.@this> Run()
    {
        var evalResult = await Evaluator.Evaluate(this);
        if (!evalResult.Success) return evalResult;
        // Both reads via the truthiness door — the value answers for itself.
        var b = await evalResult.ToBooleanAsync();
        if (await Negate.ToBooleanAsync()) b = !b;
        return Data(b);
    }
}
