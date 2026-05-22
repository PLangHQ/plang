using app;
using app.variables;
using app.modules.condition.code;

namespace app.modules.condition;

[System.ComponentModel.Description("Additional condition branch evaluated when the preceding if condition is false")]
[Example("else if %a% > 5, write 'mid'",
    "condition.elseif Left([object] %a%), Operator([operator] >), Right([int] 5)")]
[Action("elseif")]
public partial class Elseif : IContext, IStep
{
    public partial data.@this? Left { get; init; }
    public partial data.@this<Operator> Operator { get; init; }
    public partial data.@this? Right { get; init; }
    [Default(false)]
    public partial data.@this<bool> Negate { get; init; }

    [Code]
    public partial IEvaluator Evaluator { get; }

    public async Task<data.@this> Run()
    {
        var evalResult = await Evaluator.Evaluate(this);
        if (!evalResult.Success) return evalResult;
        var b = evalResult.Value is true;
        if (Negate.Value) b = !b;
        return Data(b);
    }
}
